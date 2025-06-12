# Load required libraries
library(readxl)
library(dplyr)
library(tidyr)
library(ggplot2)
library(RColorBrewer)

# Function to read XLSX file and create ranking visualization
create_ranking_plot <- function(file_path, sheet_name = NULL) {
  
  # Read the XLSX file
  if(is.null(sheet_name)) {
    data <- read_excel(file_path)
  } else {
    data <- read_excel(file_path, sheet = sheet_name)
  }
  
  # Clean column names (remove spaces and special characters)
  colnames(data) <- gsub("[^A-Za-z0-9_]", "_", colnames(data))
  
  # Extract task columns (assuming they start with "Task")
  task_columns <- grep("Task", colnames(data), value = TRUE)
  
  # Function to parse rankings from semicolon-separated strings
  parse_rankings <- function(ranking_string) {
    if(is.na(ranking_string) || ranking_string == "") return(NA)
    
    # Split by semicolon and clean
    methods <- trimws(unlist(strsplit(ranking_string, ";")))
    methods <- methods[methods != ""]
    
    # Create ranking (1st = best, 2nd = second best, etc.)
    ranking <- seq_along(methods)
    names(ranking) <- methods
    
    return(ranking)
  }
  
  # Process each task
  results_list <- list()
  
  for(task_col in task_columns) {
    task_name <- gsub("_", " ", task_col)
    
    # Extract rankings for this task
    task_data <- data[[task_col]]
    
    # Parse all rankings for this task
    all_rankings <- list()
    for(i in seq_along(task_data)) {
      participant_rankings <- parse_rankings(task_data[i])
      if(!is.null(participant_rankings) && !all(is.na(participant_rankings))) {
        all_rankings[[i]] <- participant_rankings
      }
    }
    
    # Combine all rankings into a data frame
    if(length(all_rankings) > 0) {
      # Get all unique methods
      all_methods <- unique(unlist(lapply(all_rankings, names)))
      
      # Create matrix to store rankings
      ranking_matrix <- matrix(NA, nrow = length(all_rankings), ncol = length(all_methods))
      colnames(ranking_matrix) <- all_methods
      
      # Fill the matrix
      for(i in seq_along(all_rankings)) {
        for(method in names(all_rankings[[i]])) {
          ranking_matrix[i, method] <- all_rankings[[i]][method]
        }
      }
      
      # Convert to data frame and calculate percentages
      ranking_df <- as.data.frame(ranking_matrix)
      
      # Calculate percentage for each rank and method
      rank_percentages <- data.frame()
      
      for(method in all_methods) {
        method_ranks <- ranking_df[[method]][!is.na(ranking_df[[method]])]
        if(length(method_ranks) > 0) {
          rank_counts <- table(factor(method_ranks, levels = 1:3))
          rank_props <- prop.table(rank_counts) * 100
          
          for(rank in 1:3) {
            rank_percentages <- rbind(rank_percentages, 
                                      data.frame(
                                        Task = task_name,
                                        Method = method,
                                        Rank = rank,
                                        Percentage = as.numeric(rank_props[rank])
                                      ))
          }
        }
      }
      
      results_list[[task_col]] <- rank_percentages
    }
  }
  
  # Combine all results
  final_data <- do.call(rbind, results_list)
  
  # Create the plot for each task
  plots <- list()
  
  for(task in unique(final_data$Task)) {
    task_data <- final_data[final_data$Task == task, ]
    
    # Create stacked bar chart
    p <- ggplot(task_data, aes(x = factor(Rank), y = Percentage, fill = Method)) +
      geom_bar(stat = "identity", position = "stack", color = "white", size = 0.5) +
      scale_fill_brewer(type = "qual", palette = "Set3") +
      scale_y_continuous(labels = function(x) paste0(x, "%"), limits = c(0, 100)) +
      labs(
        title = paste("Preference Ranking -", task),
        x = "Rank",
        y = "Percentage",
        fill = "Method"
      ) +
      theme_minimal() +
      theme(
        plot.title = element_text(hjust = 0.5, size = 14, face = "bold"),
        axis.title = element_text(size = 12),
        axis.text = element_text(size = 10),
        legend.title = element_text(size = 12),
        legend.text = element_text(size = 10)
      )
    
    plots[[task]] <- p
  }
  
  return(plots)
}

# Function to create combined plot (all tasks in one)
create_combined_plot <- function(file_path, sheet_name = NULL) {
  
  # Read the XLSX file
  if(is.null(sheet_name)) {
    data <- read_excel(file_path)
  } else {
    data <- read_excel(file_path, sheet = sheet_name)
  }
  
  # Clean column names
  colnames(data) <- gsub("[^A-Za-z0-9_]", "_", colnames(data))
  
  # Extract task columns
  task_columns <- grep("Task", colnames(data), value = TRUE)
  
  # Function to parse rankings
  parse_rankings <- function(ranking_string) {
    if(is.na(ranking_string) || ranking_string == "") return(NA)
    methods <- trimws(unlist(strsplit(ranking_string, ";")))
    methods <- methods[methods != ""]
    ranking <- seq_along(methods)
    names(ranking) <- methods
    return(ranking)
  }
  
  # Process all tasks and combine
  all_results <- data.frame()
  
  for(task_col in task_columns) {
    task_name <- gsub("Task\\d+_?\\(?([^)]+)\\)?", "\\1", task_col)
    task_name <- gsub("_", " ", task_name)
    
    task_data <- data[[task_col]]
    
    # Parse rankings
    all_rankings <- list()
    for(i in seq_along(task_data)) {
      participant_rankings <- parse_rankings(task_data[i])
      if(!is.null(participant_rankings) && !all(is.na(participant_rankings))) {
        all_rankings[[i]] <- participant_rankings
      }
    }
    
    if(length(all_rankings) > 0) {
      all_methods <- unique(unlist(lapply(all_rankings, names)))
      
      # Calculate percentages for each method and rank
      for(method in all_methods) {
        method_ranks <- c()
        for(ranking in all_rankings) {
          if(method %in% names(ranking)) {
            method_ranks <- c(method_ranks, ranking[method])
          }
        }
        
        if(length(method_ranks) > 0) {
          rank_counts <- table(factor(method_ranks, levels = 1:3))
          rank_props <- prop.table(rank_counts) * 100
          
          for(rank in 1:3) {
            all_results <- rbind(all_results, 
                                 data.frame(
                                   Task = task_name,
                                   Method = method,
                                   Rank = rank,
                                   Percentage = as.numeric(rank_props[rank])
                                 ))
          }
        }
      }
    }
  }
  
  # Reorder Task levels: Translation first, then Scaling, then others
  all_results$Task <- factor(all_results$Task,
                             levels = c("Translation", "Scaling",
                                        setdiff(unique(all_results$Task), c("Translation", "Scaling"))))
  
  # Create faceted plot
  p <- ggplot(all_results, aes(x = factor(Rank), y = Percentage, fill = Method)) +
    geom_bar(stat = "identity", position = "stack", color = "white", size = 0.5) +
    facet_wrap(~ Task, ncol = 3) +
    scale_fill_manual(values = c(
      "Gaze+Pinch" = "#8DD3C7",  # teal
      "HandRay" = "#FDB462",     # soft orange instead of yellow
      "Look&Drop" = "#BEBADA"    # lavender
    )) +
    scale_y_continuous(labels = function(x) paste0(x, "%"), limits = c(0, 100)) +
    labs(
      #title = "Preference Rankings by Task",
      x = "Rank",
      y = "Percentage",
      fill = "Interaction Method"
    ) +
    theme_minimal() +
    theme(
      plot.title = element_text(hjust = 0.5, size = 16, face = "bold"),
      axis.title = element_text(size = 12),
      axis.text = element_text(size = 10),
      legend.title = element_text(size = 12),
      legend.text = element_text(size = 10),
      strip.text = element_text(size = 11, face = "bold")
    )
  
  
  return(p)
}

# Example usage:
plots <- create_ranking_plot("C:/Users/Lenovo/Desktop/All/2_AUClasses/Thesis/Python/TaskRanking.xlsx")
plots[[1]]  # Show first task plot

combined_plot <- create_combined_plot("C:/Users/Lenovo/Desktop/All/2_AUClasses/Thesis/Python/TaskRanking.xlsx")
print(combined_plot)
