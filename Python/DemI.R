# Load required libraries
library(readxl)
library(dplyr)
library(ggplot2)

# Set file path to your Excel file
file_path <- r"(C:\Users\Lenovo\Desktop\All\2_AUClasses\Thesis\Python\DI.xlsx)"

# Read the Excel file (first sheet)
df <- read_excel(file_path, sheet = "Sheet1")

# Clean and rename relevant columns
df <- df %>%
  rename(
    Participant_Name = `Name2`,
    Vision_Correction = `Vision correction (e.g., glasses/contact lenses)`,
    VR_AR_Experience = `Experience with VR/AR`,
    Gaze_VR_Experience = `Experience with Gaze in VR`
  ) %>%
  select(Participant_Name, Age, Gender, Handedness, Vision_Correction, VR_AR_Experience, Gaze_VR_Experience)

# Ensure Age is numeric
df$Age <- as.numeric(df$Age)

# --- 1. Age Distribution ---
ggplot(df, aes(x = Age)) +
  geom_histogram(fill = "skyblue", bins = 8, color = "black") +
  labs(title = "Age Distribution of Participants", x = "Age", y = "Count") +
  theme_minimal()

# --- 2. Gender Distribution ---
gender_percent <- df %>%
  count(Gender) %>%
  mutate(Percent = round(n / sum(n) * 100, 1))

ggplot(gender_percent, aes(x = "", y = Percent, fill = Gender)) +
  geom_col(width = 1) +
  coord_polar("y") + 
  scale_fill_manual(values = c(
    "Female" = "#8DD3C7",      # teal
    "Male" = "#FDB462",        # soft orange
    "Non-binary" = "#BEBADA"   # lavender
  )) +
  geom_text(aes(label = paste0(Percent, "%")), 
            position = position_stack(vjust = 0.5), size = 5) +
  theme_void()


# Load additional library for reshaping
library(tidyr)

# Reshape data into long format for grouped boxplot
df_long <- df %>%
  pivot_longer(cols = c(VR_AR_Experience, Gaze_VR_Experience),
               names_to = "Experience_Type",
               values_to = "Years")

# Plot grouped boxplot
ggplot(df_long, aes(x = Experience_Type, y = Years, fill = Experience_Type)) +
  geom_boxplot() +
  labs(
       x = "Experience Type", y = "Experience") +
  scale_fill_manual(values = c("VR_AR_Experience" = "lightgreen", "Gaze_VR_Experience" = "lightskyblue")) +
  theme_minimal() +
  theme(legend.position = "none")

#--------------------------------------------------------------------------------------

library(glue)
library(tidyr)
# Ensure Age is numeric
df$Age <- as.numeric(df$Age)

# Stats for age
n_participants <- nrow(df)
age_min <- min(df$Age, na.rm = TRUE)
age_max <- max(df$Age, na.rm = TRUE)
age_mean <- round(mean(df$Age, na.rm = TRUE), 2)
age_sd <- round(sd(df$Age, na.rm = TRUE), 2)

# Gender counts with fallback
gender_counts <- table(df$Gender)
male_count <- if ("Male" %in% names(gender_counts)) gender_counts["Male"] else 0
female_count <- if ("Female" %in% names(gender_counts)) gender_counts["Female"] else 0
nonbinary_count <- sum(grepl("non", tolower(df$Gender)), na.rm = TRUE)

# Handedness
right_handed <- sum(df$Handedness == "Right", na.rm = TRUE)
left_handed <- sum(df$Handedness == "Left", na.rm = TRUE)

# VR experience counts (arbitrary threshold: >1 = experienced)
prior_vr_experience <- sum(df$VR_AR_Experience > 1, na.rm = TRUE)
no_vr_experience <- n_participants - prior_vr_experience

# Experience means & SDs
vr_exp_mean <- round(mean(df$VR_AR_Experience, na.rm = TRUE), 2)
vr_exp_sd <- round(sd(df$VR_AR_Experience, na.rm = TRUE), 2)

gaze_exp_mean <- round(mean(df$Gaze_VR_Experience, na.rm = TRUE), 2)
gaze_exp_sd <- round(sd(df$Gaze_VR_Experience, na.rm = TRUE), 2)

# Output formatted paragraph
cat(glue(
  "We recruited {n_participants} paid participants aged {age_min}-{age_max} years (\\(M = {age_mean}\\) years, \\(SD = {age_sd}\\) years). ",
  "The sample included {male_count} males, {female_count} females, and {nonbinary_count} non-binary participant. ",
  "With {right_handed} participants being right-handed and {left_handed} left-handed. ",
  "Regarding VR experience, {prior_vr_experience} participants had prior VR experience while {no_vr_experience} had no previous VR experience. ",
  "Most participants had limited VR/MR experience overall (\\(M = {vr_exp_mean}\\), \\(SD = {vr_exp_sd}\\)) ",
  "and prototyping experience (\\(M = {gaze_exp_mean}\\), \\(SD = {gaze_exp_sd}\\))."
))

#-------------------------------------------------------------
