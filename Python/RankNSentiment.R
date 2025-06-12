# ============================
# 📦 LOAD REQUIRED PACKAGES
# ============================
packages <- c("readxl", "tidyverse", "ggplot2", "dplyr", "tidyr", "RColorBrewer",
              "tidytext", "textdata", "stringr", "gridExtra")
installed <- packages %in% installed.packages()[, "Package"]
if (any(!installed)) install.packages(packages[!installed])
lapply(packages, library, character.only = TRUE)

# ============================
# 📥 LOAD EXCEL FILE
# ============================
file_path <- "C:/Users/Lenovo/Desktop/All/2_AUClasses/Thesis/Python/Ranks.xlsx"
df <- read_excel(file_path)

cat("📊 Loaded", nrow(df), "rows of data\n")
cat("📋 Columns:", paste(names(df), collapse = ", "), "\n")
print(head(df))

# ============================
# 📌 RANKING ANALYSIS
# ============================
rank_column <- "Rank the Techniques"
if (!rank_column %in% names(df)) {
  possible_names <- c("Rank the Techniques", "Ranking", "Rank", "Techniques", "Order")
  rank_column <- names(df)[grepl(paste(possible_names, collapse = "|"), names(df), ignore.case = TRUE)][1]
  if (is.na(rank_column)) stop("❌ Could not find ranking column.")
}
cat("✅ Using ranking column:", rank_column, "\n")

parse_rankings <- function(ranking_text) {
  techniques <- trimws(strsplit(ranking_text, ";")[[1]])
  data.frame(technique = techniques, rank = 1:length(techniques), stringsAsFactors = FALSE)
}

all_rankings <- data.frame()
for (i in 1:nrow(df)) {
  if (!is.na(df[[rank_column]][i]) && df[[rank_column]][i] != "") {
    participant_rankings <- parse_rankings(df[[rank_column]][i])
    participant_rankings$participant_id <- i
    all_rankings <- rbind(all_rankings, participant_rankings)
  }
}

all_rankings$technique_clean <- case_when(
  grepl("gaze.*pinch|pinch.*gaze", all_rankings$technique, ignore.case = TRUE) ~ "Gaze+Pinch",
  grepl("handray|hand.*ray", all_rankings$technique, ignore.case = TRUE) ~ "HandRay",
  grepl("look.*drop|drop.*look", all_rankings$technique, ignore.case = TRUE) ~ "Look&Drop",
  TRUE ~ all_rankings$technique
)

ranking_summary <- all_rankings %>%
  group_by(technique_clean, rank) %>%
  summarise(count = n(), .groups = 'drop') %>%
  group_by(technique_clean) %>%
  mutate(total = sum(count), percentage = (count / total) * 100) %>%
  ungroup()

complete_rankings <- ranking_summary %>%
  complete(technique_clean, rank = 1:3, fill = list(count = 0, percentage = 0)) %>%
  group_by(technique_clean) %>%
  mutate(total = max(total, na.rm = TRUE)) %>%
  mutate(percentage = ifelse(total > 0, (count / total) * 100, 0)) %>%
  ungroup()

# 📊 Plot Ranking Distribution
rank_colors <- c("1" = "#FF69B4", "2" = "#87CEEB", "3" = "#9ACD32")
p1 <- ggplot(complete_rankings, aes(x = technique_clean, y = percentage, fill = factor(rank))) +
  geom_bar(stat = "identity", position = "stack", color = "white", size = 0.3) +
  scale_fill_manual(values = rank_colors, name = "Rank", labels = c("1st", "2nd", "3rd")) +
  labs( x = "Technique", y = "Percentage") +
  theme_minimal()

print(p1)

# 📊 Plot Average Rank
avg_ranks <- all_rankings %>%
  group_by(technique_clean) %>%
  summarise(
    avg_rank = mean(rank), 
    first_place_pct = mean(rank == 1) * 100, 
    .groups = 'drop'
  ) %>%
  arrange(avg_rank)



# ============================
# 💬 SENTIMENT ANALYSIS
# ============================
feedback_column <- names(df)[ncol(df)]
for (col in names(df)) {
  if (any(grepl("feedback|comment|response|review", tolower(col)))) {
    feedback_column <- col
    break
  }
}
cat("✅ Feedback Column:", feedback_column, "\n")

df$cleaned_feedback <- sapply(df[[feedback_column]], function(text) {
  if (is.na(text) || text == "") return("")
  text <- str_to_lower(text)
  text <- str_replace_all(text, "[^a-zA-Z\\s]", "")
  str_squish(text)
})

afinn <- get_sentiments("afinn")
bing <- get_sentiments("bing")
nrc <- get_sentiments("nrc")

calculate_sentiment <- function(text_data) {
  text_df <- data.frame(id = 1:length(text_data), text = text_data)
  words_df <- text_df %>% unnest_tokens(word, text) %>% filter(!is.na(word), word != "")
  
  afinn_scores <- words_df %>%
    inner_join(afinn, by = "word") %>%
    group_by(id) %>%
    summarise(afinn_score = sum(value), word_count = n(), .groups = 'drop') %>%
    mutate(afinn_avg = afinn_score / word_count)
  
  bing_scores <- words_df %>%
    inner_join(bing, by = "word") %>%
    count(id, sentiment) %>%
    pivot_wider(names_from = sentiment, values_from = n, values_fill = 0) %>%
    mutate(
      bing_score = positive - negative,
      total_words = positive + negative,
      bing_ratio = ifelse(total_words > 0, bing_score / total_words, 0)
    )
  
  nrc_scores <- words_df %>%
    inner_join(nrc, by = "word") %>%
    filter(sentiment %in% c("positive", "negative")) %>%
    count(id, sentiment) %>%
    pivot_wider(names_from = sentiment, values_from = n, values_fill = 0) %>%
    mutate(
      nrc_score = positive - negative,
      nrc_total = positive + negative,
      nrc_ratio = ifelse(nrc_total > 0, nrc_score / nrc_total, 0)
    )
  
  sentiment_results <- full_join(afinn_scores, bing_scores, by = "id") %>%
    full_join(nrc_scores, by = "id") %>%
    replace(is.na(.), 0)
  
  sentiment_results
}

sentiment_df <- calculate_sentiment(df$cleaned_feedback)

# Combine with main data
df <- bind_cols(df, sentiment_df %>% select(-id))

# Composite sentiment score
safe_scale <- function(x) if(sd(x, na.rm = TRUE) == 0) rep(0, length(x)) else as.vector(scale(x))

df$composite_score <- (
  safe_scale(df$afinn_avg) * 0.4 + 
    safe_scale(df$bing_ratio) * 0.3 + 
    safe_scale(df$nrc_ratio) * 0.3
) / 3

df$sentiment_category <- case_when(
  df$composite_score > 0.1 ~ "Positive",
  df$composite_score < -0.1 ~ "Negative",
  TRUE ~ "Neutral"
)

cat("\n📊 Sentiment distribution:\n")
print(table(df$sentiment_category))
print(round(prop.table(table(df$sentiment_category)) * 100, 1))

# 📊 Sentiment Boxplot
df_long <- df %>%
  select(composite_score, afinn_avg, bing_ratio, nrc_ratio, sentiment_category) %>%
  pivot_longer(cols = -sentiment_category, names_to = "metric", values_to = "value")

ggplot(df_long, aes(x = metric, y = value, fill = sentiment_category)) +
  geom_boxplot(alpha = 0.7, outlier.shape = 16, outlier.size = 1.5) +
  scale_fill_brewer(type = "qual", palette = "Set2") +
  labs(
    #title = "Comparison of All Sentiment Score Methods",
    x = "Sentiment Analysis Method",
    y = "Score Value",
    fill = "Method"
  ) +
  theme_minimal() +
  theme(
    plot.title = element_text(hjust = 0.5, size = 14, face = "bold"),
    axis.text.x = element_text(angle = 0),
    legend.position = "top"
  ) +
  geom_hline(yintercept = 0, linetype = "dashed", alpha = 0.5)