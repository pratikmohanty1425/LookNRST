# Load required libraries
library(readxl)
library(tidyverse)
library(ggplot2)
library(dplyr)
library(tidyr)
library(RColorBrewer)

# Load your Excel file - UPDATE THIS PATH
file_path <- "C:/Users/Lenovo/Desktop/All/2_AUClasses/Thesis/Python/Ranks.xlsx"
df <- read_excel(file_path)

cat("📊 Loaded", nrow(df), "rows of data\n")
cat("📋 Columns:", paste(names(df), collapse = ", "), "\n")

# Display first few rows
cat("📋 First few rows:\n")
print(head(df))

# Find the ranking column
rank_column <- "Rank the Techniques"
if (!rank_column %in% names(df)) {
  possible_names <- c("Rank the Techniques", "Ranking", "Rank", "Techniques", "Order")
  rank_column <- names(df)[grepl(paste(possible_names, collapse = "|"), names(df), ignore.case = TRUE)][1]
  
  if (is.na(rank_column)) {
    stop("❌ Could not find ranking column. Please check column names.")
  }
}
cat("✅ Using ranking column:", rank_column, "\n")

# Function to parse ranking data
parse_rankings <- function(ranking_text) {
  techniques <- strsplit(ranking_text, ";")[[1]]
  techniques <- trimws(techniques)
  data.frame(
    technique = techniques,
    rank = 1:length(techniques),
    stringsAsFactors = FALSE
  )
}

# Parse all rankings
cat("\n🔄 Parsing ranking data...\n")
all_rankings <- data.frame()
for (i in 1:nrow(df)) {
  if (!is.na(df[[rank_column]][i]) && df[[rank_column]][i] != "") {
    participant_rankings <- parse_rankings(df[[rank_column]][i])
    participant_rankings$participant_id <- i
    all_rankings <- rbind(all_rankings, participant_rankings)
  }
}

# Standardize technique names
all_rankings$technique_clean <- case_when(
  grepl("gaze.*pinch|pinch.*gaze", all_rankings$technique, ignore.case = TRUE) ~ "Gaze+Pinch",
  grepl("handray|hand.*ray", all_rankings$technique, ignore.case = TRUE) ~ "HandRay",
  grepl("look.*drop|drop.*look", all_rankings$technique, ignore.case = TRUE) ~ "Look&Drop",
  TRUE ~ all_rankings$technique
)
cat("✅ Found techniques:", paste(unique(all_rankings$technique_clean), collapse = ", "), "\n")

# Calculate ranking distribution
ranking_summary <- all_rankings %>%
  group_by(technique_clean, rank) %>%
  summarise(count = n(), .groups = 'drop') %>%
  group_by(technique_clean) %>%
  mutate(
    total = sum(count),
    percentage = (count / total) * 100
  ) %>%
  ungroup()

# Fill missing combinations
complete_rankings <- ranking_summary %>%
  complete(technique_clean, rank = 1:3, fill = list(count = 0, percentage = 0)) %>%
  group_by(technique_clean) %>%
  mutate(total = max(total, na.rm = TRUE)) %>%
  mutate(percentage = ifelse(total > 0, (count / total) * 100, 0)) %>%
  ungroup()

cat("\n=== RANKING SUMMARY ===\n")
print(complete_rankings)

# === ✅ Plot: Switched to Rank on X-axis, Technique as fill ===
technique_colors <- c(
  "Gaze+Pinch" = "#8DD3C7",
  "HandRay" = "#FDB462",
  "Look&Drop" = "#BEBADA"
)

p1 <- ggplot(complete_rankings, aes(x = factor(rank), y = percentage, fill = technique_clean)) +
  geom_bar(stat = "identity", position = "stack", color = "white", size = 0.3) +
  scale_fill_manual(
    values = technique_colors,
    name = "Technique"
  ) +
  scale_y_continuous(
    labels = function(x) paste0(x, "%"),
    breaks = seq(0, 100, 25),
    limits = c(0, 100)
  ) +
  labs(
    x = "Rank Position",
    y = "Percentage",
    caption = paste("Based on", length(unique(all_rankings$participant_id)), "participants")
  ) +
  theme_minimal() +
  theme(
    plot.title = element_text(hjust = 0.5, size = 16, face = "bold"),
    plot.subtitle = element_text(hjust = 0.5, size = 12, color = "gray60"),
    axis.text.x = element_text(angle = 0, hjust = 0.5, size = 11),
    axis.text.y = element_text(size = 10),
    axis.title = element_text(size = 12, face = "bold"),
    legend.title = element_text(size = 11, face = "bold"),
    legend.text = element_text(size = 10),
    panel.grid.major.x = element_blank(),
    panel.grid.minor = element_blank()
  )
print(p1)

# === Summary statistics ===
avg_ranks <- all_rankings %>%
  group_by(technique_clean) %>%
  summarise(
    avg_rank = mean(rank),
    median_rank = median(rank),
    total_votes = n(),
    first_place_votes = sum(rank == 1),
    first_place_pct = round((first_place_votes / total_votes) * 100, 1),
    .groups = 'drop'
  ) %>%
  arrange(avg_rank)

cat("\n📊 TECHNIQUE PERFORMANCE:\n")
for (i in 1:nrow(avg_ranks)) {
  tech <- avg_ranks$technique_clean[i]
  avg <- round(avg_ranks$avg_rank[i], 2)
  first_pct <- avg_ranks$first_place_pct[i]
  cat("•", tech, ": Avg rank =", avg, ", 1st place =", first_pct, "%\n")
}

# Optional export
# write.csv(complete_rankings, "technique_rankings_analysis.csv", row.names = FALSE)
# write.csv(avg_ranks, "technique_average_ranks.csv", row.names = FALSE)

cat("\n✅ Analysis complete!\n")
