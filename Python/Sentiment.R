# R Sentiment Analysis for User Feedback with Boxplots - FIXED VERSION
# Install required packages if not already installed
if (!require("readxl")) install.packages("readxl")
if (!require("tidyverse")) install.packages("tidyverse")
if (!require("tidytext")) install.packages("tidytext")
if (!require("textdata")) install.packages("textdata")
if (!require("ggplot2")) install.packages("ggplot2")
if (!require("dplyr")) install.packages("dplyr")
if (!require("stringr")) install.packages("stringr")
if (!require("gridExtra")) install.packages("gridExtra")

# Load required libraries
library(readxl)
library(tidyverse)
library(tidytext)
library(textdata)
library(ggplot2)
library(dplyr)
library(stringr)
library(gridExtra)

# Load your Excel file
file_path <- "C:/Users/Lenovo/Desktop/All/2_AUClasses/Thesis/Python/App.xlsx"
df <- read_excel(file_path)

cat("📊 Loaded", nrow(df), "rows of data\n")
cat("📋 Columns:", paste(names(df), collapse = ", "), "\n")

# Display first few rows
cat("📋 First few rows:\n")
print(head(df))

# Identify column names automatically
cat("\n🔍 Identifying column names...\n")

# Find ID column
id_column <- names(df)[1]  # Default to first column
for (col in names(df)) {
  if (any(grepl("id|number|index", tolower(col)))) {
    id_column <- col
    break
  }
}

# Find feedback column
feedback_column <- names(df)[ncol(df)]  # Default to last column
for (col in names(df)) {
  if (any(grepl("feedback|comment|response|review", tolower(col)))) {
    feedback_column <- col
    break
  }
}

cat("✅ ID Column:", id_column, "\n")
cat("✅ Feedback Column:", feedback_column, "\n")

# Clean text function
clean_text <- function(text) {
  if (is.na(text) || text == "") return("")
  
  # Convert to lowercase and remove special characters
  text <- str_to_lower(text)
  text <- str_replace_all(text, "[^a-zA-Z\\s]", "")
  
  # Remove extra whitespace
  text <- str_squish(text)
  
  return(text)
}

# Clean the feedback data
df$cleaned_feedback <- sapply(df[[feedback_column]], clean_text)

# Get sentiment lexicons with error handling
tryCatch({
  afinn <- get_sentiments("afinn")
  bing <- get_sentiments("bing")
  nrc <- get_sentiments("nrc")
  cat("✅ Sentiment lexicons loaded successfully\n")
}, error = function(e) {
  cat("❌ Error loading sentiment lexicons:", e$message, "\n")
  cat("💡 Try running: textdata::lexicon_afinn(), textdata::lexicon_bing(), textdata::lexicon_nrc()\n")
  stop("Cannot proceed without sentiment lexicons")
})

# FIXED Function to calculate sentiment scores
calculate_sentiment <- function(text_data) {
  # Create a data frame with text and ID
  text_df <- data.frame(
    id = 1:length(text_data),
    text = text_data,
    stringsAsFactors = FALSE
  )
  
  # Tokenize the text
  words_df <- text_df %>%
    unnest_tokens(word, text) %>%
    filter(!is.na(word), word != "")
  
  # AFINN sentiment (numeric scores)
  afinn_scores <- words_df %>%
    inner_join(afinn, by = "word") %>%
    group_by(id) %>%
    summarise(
      afinn_score = sum(value, na.rm = TRUE),
      word_count = n(),
      .groups = 'drop'
    ) %>%
    mutate(afinn_avg = ifelse(word_count > 0, afinn_score / word_count, 0))
  
  # FIXED: Bing sentiment (positive/negative counts) with proper column handling
  bing_scores <- words_df %>%
    inner_join(bing, by = "word") %>%
    group_by(id, sentiment) %>%
    summarise(n = n(), .groups = 'drop') %>%
    pivot_wider(names_from = sentiment, values_from = n, values_fill = 0) %>%
    mutate(
      # Handle missing columns by checking if they exist
      positive = if("positive" %in% names(.)) positive else 0,
      negative = if("negative" %in% names(.)) negative else 0,
      bing_score = positive - negative,
      total_words = positive + negative,
      bing_ratio = ifelse(total_words > 0, bing_score / total_words, 0)
    )
  
  # FIXED: NRC sentiment with many-to-many relationship handling
  nrc_scores <- words_df %>%
    inner_join(nrc, by = "word", relationship = "many-to-many") %>%
    filter(sentiment %in% c("positive", "negative")) %>%
    group_by(id, sentiment) %>%
    summarise(n = n(), .groups = 'drop') %>%
    pivot_wider(names_from = sentiment, values_from = n, values_fill = 0) %>%
    mutate(
      # Handle missing columns by checking if they exist
      positive = if("positive" %in% names(.)) positive else 0,
      negative = if("negative" %in% names(.)) negative else 0,
      nrc_score = positive - negative,
      nrc_total = positive + negative,
      nrc_ratio = ifelse(nrc_total > 0, nrc_score / nrc_total, 0)
    )
  
  # Combine all sentiment scores with proper column handling
  all_ids <- data.frame(id = 1:length(text_data))
  
  sentiment_results <- all_ids %>%
    left_join(afinn_scores, by = "id") %>%
    left_join(bing_scores, by = "id") %>%
    left_join(nrc_scores, by = "id") %>%
    mutate(
      # Handle AFINN columns
      afinn_score = ifelse(is.na(afinn_score), 0, afinn_score),
      afinn_avg = ifelse(is.na(afinn_avg), 0, afinn_avg),
      word_count = ifelse(is.na(word_count), 0, word_count),
      
      # Handle Bing columns
      bing_score = ifelse(is.na(bing_score), 0, bing_score),
      bing_ratio = ifelse(is.na(bing_ratio), 0, bing_ratio),
      total_words = ifelse(is.na(total_words), 0, total_words),
      
      # Handle NRC columns
      nrc_score = ifelse(is.na(nrc_score), 0, nrc_score),
      nrc_ratio = ifelse(is.na(nrc_ratio), 0, nrc_ratio),
      nrc_total = ifelse(is.na(nrc_total), 0, nrc_total)
    )
  
  # Ensure positive/negative columns exist for both Bing and NRC
  if(!"positive" %in% names(sentiment_results)) {
    sentiment_results$positive <- 0
  }
  if(!"negative" %in% names(sentiment_results)) {
    sentiment_results$negative <- 0
  }
  
  # Replace NA values in positive/negative columns
  sentiment_results <- sentiment_results %>%
    mutate(
      positive = ifelse(is.na(positive), 0, positive),
      negative = ifelse(is.na(negative), 0, negative)
    )
  
  return(sentiment_results)
}

# Calculate sentiment scores
cat("\n🔄 Calculating sentiment scores...\n")
sentiment_results <- calculate_sentiment(df$cleaned_feedback)

# Add sentiment results to original dataframe
df <- cbind(df, sentiment_results[, -1])  # Remove the id column

# Create composite sentiment score (normalized between -1 and 1)
# Use safe scaling that handles constant values
safe_scale <- function(x) {
  if(sd(x, na.rm = TRUE) == 0) {
    return(rep(0, length(x)))
  } else {
    return(as.vector(scale(x)))
  }
}

df$composite_score <- (
  safe_scale(df$afinn_avg) * 0.4 + 
    safe_scale(df$bing_ratio) * 0.3 + 
    safe_scale(df$nrc_ratio) * 0.3
) / 3

# Categorize sentiment
df$sentiment_category <- case_when(
  df$composite_score > 0.1 ~ "Positive",
  df$composite_score < -0.1 ~ "Negative",
  TRUE ~ "Neutral"
)

# Display results
cat("\n=== SENTIMENT ANALYSIS RESULTS ===\n")
cat("Sample of analyzed feedback:\n")
sample_cols <- c(id_column, feedback_column, "composite_score", "sentiment_category")
available_cols <- sample_cols[sample_cols %in% names(df)]
print(df[1:min(6, nrow(df)), available_cols])

cat("\n=== OVERALL SENTIMENT DISTRIBUTION ===\n")
sentiment_counts <- table(df$sentiment_category)
print(sentiment_counts)
print(prop.table(sentiment_counts) * 100)

cat("\n=== DETAILED STATISTICS ===\n")
cat("Average Composite Score:", round(mean(df$composite_score, na.rm = TRUE), 3), "\n")
cat("Average AFINN Score:", round(mean(df$afinn_avg, na.rm = TRUE), 3), "\n")
cat("Average Bing Ratio:", round(mean(df$bing_ratio, na.rm = TRUE), 3), "\n")
cat("Average NRC Ratio:", round(mean(df$nrc_ratio, na.rm = TRUE), 3), "\n")

# Create comprehensive boxplots
cat("\n📊 Creating boxplot visualizations...\n")

# Prepare data for plotting - only include columns that exist
plot_cols <- c("composite_score", "afinn_avg", "bing_ratio", "nrc_ratio")
available_plot_cols <- plot_cols[plot_cols %in% names(df)]

if(length(available_plot_cols) > 0) {
  plot_data_long <- df %>%
    select(all_of(c(id_column)), sentiment_category, all_of(available_plot_cols)) %>%
    pivot_longer(
      cols = all_of(available_plot_cols),
      names_to = "score_type",
      values_to = "score_value"
    ) %>%
    mutate(
      score_type = case_when(
        score_type == "composite_score" ~ "Composite Score",
        score_type == "afinn_avg" ~ "AFINN Average",
        score_type == "bing_ratio" ~ "Bing Ratio",
        score_type == "nrc_ratio" ~ "NRC Ratio",
        TRUE ~ score_type
      )
    )
  
  # 1. Boxplot of Composite Scores by Sentiment Category
  p1 <- ggplot(df, aes(x = sentiment_category, y = composite_score, fill = sentiment_category)) +
    geom_boxplot(alpha = 0.7, outlier.shape = 16, outlier.size = 2) +
    scale_fill_manual(values = c("Positive" = "#2ecc71", "Negative" = "#e74c3c", "Neutral" = "#95a5a6")) +
    labs(
      title = "Composite Sentiment Scores by Category",
      x = "Sentiment Category",
      y = "Composite Score",
      fill = "Category"
    ) +
    theme_minimal() +
    theme(
      plot.title = element_text(hjust = 0.5, size = 14, face = "bold"),
      axis.text.x = element_text(angle = 0),
      legend.position = "none"
    ) +
    geom_hline(yintercept = 0, linetype = "dashed", alpha = 0.5)
  
  # 2. Boxplot comparing all score types
  p2 <- ggplot(plot_data_long, aes(x = score_type, y = score_value, fill = score_type)) +
    geom_boxplot(alpha = 0.7, outlier.shape = 16, outlier.size = 1.5) +
    scale_fill_manual(values = c(
      "Composite Score" = "#3498db",
      "AFINN Average" = "#9b59b6", 
      "Bing Ratio" = "#f39c12",
      "NRC Ratio" = "#1abc9c"
    )) +
    labs(
      title = "Distribution of Different Sentiment Scores",
      x = "Score Type",
      y = "Score Value",
      fill = "Score Type"
    ) +
    theme_minimal() +
    theme(
      plot.title = element_text(hjust = 0.5, size = 14, face = "bold"),
      axis.text.x = element_text(angle = 45, hjust = 1),
      legend.position = "none"
    ) +
    geom_hline(yintercept = 0, linetype = "dashed", alpha = 0.5)
  
  # 3. Faceted boxplot by sentiment category for all score types
  p3 <- ggplot(plot_data_long, aes(x = sentiment_category, y = score_value, fill = sentiment_category)) +
    geom_boxplot(alpha = 0.7, outlier.shape = 16, outlier.size = 1) +
    facet_wrap(~score_type, scales = "free_y", ncol = 2) +
    scale_fill_manual(values = c("Positive" = "#2ecc71", "Negative" = "#e74c3c", "Neutral" = "#95a5a6")) +
    labs(
      title = "Sentiment Scores by Category and Type",
      x = "Sentiment Category",
      y = "Score Value",
      fill = "Category"
    ) +
    theme_minimal() +
    theme(
      plot.title = element_text(hjust = 0.5, size = 14, face = "bold"),
      axis.text.x = element_text(angle = 45, hjust = 1),
      strip.text = element_text(face = "bold")
    ) +
    geom_hline(yintercept = 0, linetype = "dashed", alpha = 0.3)
  
  # 4. Individual score distributions
  p4 <- df %>%
    select(all_of(available_plot_cols)) %>%
    pivot_longer(everything(), names_to = "score_type", values_to = "score_value") %>%
    mutate(
      score_type = case_when(
        score_type == "composite_score" ~ "Composite",
        score_type == "afinn_avg" ~ "AFINN",
        score_type == "bing_ratio" ~ "Bing",
        score_type == "nrc_ratio" ~ "NRC",
        TRUE ~ score_type
      )
    ) %>%
    ggplot(aes(x = score_type, y = score_value, fill = score_type)) +
    geom_boxplot(alpha = 0.7, outlier.shape = 16, outlier.size = 1.5) +
    scale_fill_brewer(type = "qual", palette = "Set2") +
    labs(
      title = "Comparison of All Sentiment Score Methods",
      x = "Sentiment Analysis Method",
      y = "Score Value",
      fill = "Method"
    ) +
    theme_minimal() +
    theme(
      plot.title = element_text(hjust = 0.5, size = 14, face = "bold"),
      axis.text.x = element_text(angle = 0),
      legend.position = "none"
    ) +
    geom_hline(yintercept = 0, linetype = "dashed", alpha = 0.5)
  
  # Display all plots
  tryCatch({
    grid.arrange( p3, p4, ncol = 2, nrow = 1)
  }, error = function(e) {
    cat("⚠️ Error creating combined plot, showing individual plots instead\n")
    #print(p1)
    #print(p2)
    print(p3)
    print(p4)
  })
} else {
  cat("⚠️ No score columns available for plotting\n")
}

# Find most positive and negative feedback (if data exists)
if(nrow(df) > 0 && !all(is.na(df$composite_score))) {
  cat("\n=== MOST POSITIVE FEEDBACK ===\n")
  most_positive_idx <- which.max(df$composite_score)
  cat("Score:", round(df$composite_score[most_positive_idx], 3), "\n")
  cat("Feedback:", df[[feedback_column]][most_positive_idx], "\n")
  
  cat("\n=== MOST NEGATIVE FEEDBACK ===\n")
  most_negative_idx <- which.min(df$composite_score)
  cat("Score:", round(df$composite_score[most_negative_idx], 3), "\n")
  cat("Feedback:", df[[feedback_column]][most_negative_idx], "\n")
}

# Key insights
cat("\n=== KEY INSIGHTS ===\n")
total_responses <- nrow(df)
positive_pct <- round((sum(df$sentiment_category == "Positive") / total_responses) * 100, 1)
negative_pct <- round((sum(df$sentiment_category == "Negative") / total_responses) * 100, 1)
neutral_pct <- round((sum(df$sentiment_category == "Neutral") / total_responses) * 100, 1)

cat("•", positive_pct, "% of feedback is positive\n")
cat("•", negative_pct, "% of feedback is negative\n")
cat("•", neutral_pct, "% of feedback is neutral\n")

overall_sentiment <- ifelse(
  mean(df$composite_score, na.rm = TRUE) > 0.1, "positive",
  ifelse(mean(df$composite_score, na.rm = TRUE) < -0.1, "negative", "neutral")
)
cat("• Overall sentiment is", overall_sentiment, "\n")
cat("• Average sentiment strength:", round(abs(mean(df$composite_score, na.rm = TRUE)), 3), "\n")

# Export results
tryCatch({
  write.csv(df, "sentiment_analysis_results_R.csv", row.names = FALSE)
  cat("\n✅ Results exported to 'sentiment_analysis_results_R.csv'\n")
}, error = function(e) {
  cat("\n⚠️ Could not export CSV:", e$message, "\n")
})

cat("\n🎉 Sentiment analysis complete!\n")