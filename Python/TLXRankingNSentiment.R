# ============================
# 📦 Load required libraries
# ============================
library(tidyverse)
library(readxl)
library(ggpubr)
library(tidytext)
library(textdata)

# ============================
# 📄 Load Excel Data
# ============================
df <- read_excel("C:/Users/Lenovo/Desktop/All/2_AUClasses/Thesis/Python/test.xlsx")

# ============================
# 🎯 Preprocess TLX Columns
# ============================
df <- df %>%
  select(Technique,
         `Mental Demand`, `Physical Demand`, `Temporal Demand`,
         Performance, Effort, Frustration, everything()) %>%
  rename(Mental = `Mental Demand`,
         Physical = `Physical Demand`,
         Temporal = `Temporal Demand`)

# Compute TLX total score
df <- df %>%
  mutate(`TLX total` = rowMeans(across(c(Mental, Physical, Temporal, Performance, Effort, Frustration))))

# ============================
# 📊 TLX Plot (Median Scores)
# ============================
tlx_long <- df %>%
  pivot_longer(cols = c(Mental, Physical, Temporal, Performance, Effort, Frustration, `TLX total`),
               names_to = "Category", values_to = "Score")

tlx_plot <- ggplot(tlx_long, aes(x = Category, y = Score, fill = Technique)) +
  stat_summary(fun = median, geom = "bar", position = position_dodge(0.8), width = 0.7) +
  stat_summary(fun.data = median_hilow, geom = "errorbar",
               position = position_dodge(0.8), width = 0.2) +
  scale_y_continuous(breaks = 1:7, limits = c(0, 7)) +
  labs(y = "Score", x = NULL) +
  theme_minimal(base_size = 14) +
  theme(axis.text.x = element_text(angle = 0, hjust = 0.5),
        legend.position = "top") +
  scale_fill_brewer(palette = "Set2", breaks = c("Technique A", "Technique B", "Technique C")) # Customize order here

print(tlx_plot)

# ============================
# 💬 Sentiment Analysis
# ============================

# Identify feedback column
feedback_column <- names(df)[ncol(df)]
for (col in names(df)) {
  if (any(grepl("feedback|comment|response|review", tolower(col)))) {
    feedback_column <- col
    break
  }
}
cat("✅ Feedback Column:", feedback_column, "\n")

# Clean text
df$cleaned_feedback <- sapply(df[[feedback_column]], function(text) {
  if (is.na(text) || text == "") return("")
  text <- str_to_lower(text)
  text <- str_replace_all(text, "[^a-zA-Z\\s]", "")
  str_squish(text)
})

# Load lexicons
afinn <- get_sentiments("afinn")
bing <- get_sentiments("bing")
nrc <- get_sentiments("nrc")

# Calculate sentiment
calculate_sentiment <- function(text_data) {
  text_df <- data.frame(id = 1:length(text_data), text = text_data)
  words_df <- text_df %>%
    unnest_tokens(word, text) %>%
    filter(!is.na(word), word != "")
  
  afinn_scores <- words_df %>%
    inner_join(afinn, by = "word") %>%
    group_by(id) %>%
    summarise(afinn_score = sum(value), word_count = n(), .groups = 'drop') %>%
    mutate(afinn_avg = afinn_score / word_count)
  
  bing_scores <- words_df %>%
    inner_join(bing, by = "word") %>%
    count(id, sentiment) %>%
    pivot_wider(names_from = sentiment, values_from = n, values_fill = 0) %>%
    mutate(bing_score = positive - negative,
           total_words = positive + negative,
           bing_ratio = ifelse(total_words > 0, bing_score / total_words, 0))
  
  nrc_scores <- words_df %>%
    inner_join(nrc, by = "word") %>%
    filter(sentiment %in% c("positive", "negative")) %>%
    count(id, sentiment) %>%
    pivot_wider(names_from = sentiment, values_from = n, values_fill = 0) %>%
    mutate(nrc_score = positive - negative,
           nrc_total = positive + negative,
           nrc_ratio = ifelse(nrc_total > 0, nrc_score / nrc_total, 0))
  
  sentiment_results <- full_join(afinn_scores, bing_scores, by = "id") %>%
    full_join(nrc_scores, by = "id") %>%
    replace(is.na(.), 0)
  
  sentiment_results
}

sentiment_df <- calculate_sentiment(df$cleaned_feedback)

# Merge sentiment scores
df <- bind_cols(df, sentiment_df %>% select(-id))

# Composite score (individual-level)
safe_scale <- function(x) if (sd(x, na.rm = TRUE) == 0) rep(0, length(x)) else as.vector(scale(x))

df$composite_score <- (
  safe_scale(df$afinn_avg) * 0.4 +
    safe_scale(df$bing_ratio) * 0.3 +
    safe_scale(df$nrc_ratio) * 0.3
) / 3

# Sentiment category (individual)
df$sentiment_category <- case_when(
  df$composite_score > 0.1 ~ "Positive",
  df$composite_score < -0.1 ~ "Negative",
  TRUE ~ "Neutral"
)

# 📊 Sentiment distribution (overall)
cat("\n📊 Overall Sentiment distribution:\n")
print(table(df$sentiment_category))
print(round(prop.table(table(df$sentiment_category)) * 100, 1))

# 📊 Sentiment breakdown by technique
grouped_sentiment <- df %>%
  group_by(Technique) %>%
  summarise(
    afinn_avg = mean(afinn_avg, na.rm = TRUE),
    bing_ratio = mean(bing_ratio, na.rm = TRUE),
    nrc_ratio = mean(nrc_ratio, na.rm = TRUE)
  ) %>%
  mutate(
    composite_score = (afinn_avg * 0.4 + bing_ratio * 0.3 + nrc_ratio * 0.3),
    sentiment_category = case_when(
      composite_score > 0.1 ~ "Positive",
      composite_score < -0.1 ~ "Negative",
      TRUE ~ "Neutral"
    )
  )

print(grouped_sentiment)

# 📦 Reshape for boxplot
df_long <- df %>%
  select(Technique, composite_score, afinn_avg, bing_ratio, nrc_ratio, sentiment_category) %>%
  pivot_longer(cols = c(composite_score, afinn_avg, bing_ratio, nrc_ratio),
               names_to = "metric", values_to = "value")

# 📊 Boxplot of individual sentiment scores
ggplot(df_long, aes(x = metric, y = value, fill = sentiment_category)) +
  geom_boxplot(alpha = 0.7, outlier.shape = 16, outlier.size = 1.5) +
  scale_fill_brewer(type = "qual", palette = "Set2",
                    breaks = c("Positive", "Neutral", "Negative")) +
  labs(x = "Sentiment Metric", y = "Score Value", fill = "Sentiment") +
  theme_minimal() +
  theme(
    axis.text.x = element_text(angle = 0),
    legend.position = "top"
  ) +
  geom_hline(yintercept = 0, linetype = "dashed", alpha = 0.5)

# 📊 Barplot: Sentiment distribution by technique
ggplot(df, aes(x = Technique, fill = sentiment_category)) +
  geom_bar(position = "fill") +
  scale_y_continuous(labels = scales::percent) +
  scale_fill_brewer(palette = "Set2", breaks = c("Positive", "Neutral", "Negative")) +
  labs(y = "Proportion", x = "Technique", fill = "Sentiment") +
  theme_minimal() +
  theme(legend.position = "top")
# 📦 Reshape for sentiment analysis by Technique
df_long_technique_sentiment <- df %>%
  select(Technique, sentiment_category, afinn_avg, bing_ratio, nrc_ratio, composite_score) %>%
  pivot_longer(cols = c(afinn_avg, bing_ratio, nrc_ratio, composite_score),
               names_to = "metric", values_to = "value")

# 📊 Plot one graph per technique
unique_techniques <- unique(df$Technique)

for (tech in unique_techniques) {
  plot_data <- df_long_technique_sentiment %>% filter(Technique == tech)
  
  p <- ggplot(plot_data, aes(x = metric, y = value, fill = sentiment_category)) +
    geom_boxplot(alpha = 0.7, outlier.shape = 16, outlier.size = 1.5) +
    scale_fill_manual(values = c("Positive" = "#66C2A5", "Neutral" = "#FC8D62", "Negative" = "#8DA0CB")) +
    labs(
      title = paste("Sentiment Distribution -", tech),
      x = "Sentiment Metric",
      y = "Score Value",
      fill = "Sentiment"
    ) +
    theme_minimal(base_size = 14) +
    theme(
      plot.title = element_text(hjust = 0.5, size = 16, face = "bold"),
      axis.text.x = element_text(angle = 0),
      legend.position = "top"
    ) +
    geom_hline(yintercept = 0, linetype = "dashed", alpha = 0.5)
  
  print(p)
}

# 📦 Reshape data for grouped boxplot (metric × sentiment × technique)
df_long <- df %>%
  select(Technique, sentiment_category, afinn_avg, bing_ratio, nrc_ratio, composite_score) %>%
  pivot_longer(cols = c(afinn_avg, bing_ratio, nrc_ratio, composite_score),
               names_to = "metric", values_to = "value")

# 🔁 Reorder facet labels
df_long$sentiment_category <- factor(df_long$sentiment_category,
                                     levels = c("Positive", "Neutral", "Negative"))


# 📊 Grouped boxplot: Technique × Sentiment × Metric
ggplot(df_long, aes(x = metric, y = value, fill = Technique)) +
  geom_boxplot(position = position_dodge(0.8), width = 0.7, outlier.shape = 16, outlier.size = 1.5) +
  facet_wrap(~sentiment_category, nrow = 1) +
  scale_fill_brewer(palette = "Set2") +
  labs(
    x = "Sentiment Metric",
    y = "Score Value",
    fill = "Technique"
  ) +
  theme_minimal(base_size = 14) +
  theme(
    plot.title = element_text(hjust = 0.5, face = "bold"),
    axis.text.x = element_text(angle = 0),
    legend.position = "top"
  ) +
  geom_hline(yintercept = 0, linetype = "dashed", alpha = 0.5)
write_csv(df_long, "df_long_sentiment_analysis.csv")

