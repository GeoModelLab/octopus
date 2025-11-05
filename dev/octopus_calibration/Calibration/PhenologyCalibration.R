# PHENOLOGY CALIBRATION
# 1. Compare optimization parameters with setted parameters (for each file)
# 2. Plot output file for each site comparing bbch simulated and reference bbch

# ---
#Set up
# ---

rm(list = ls())

#this path
setwd(dirname(rstudioapi::getActiveDocumentContext()$path))

# Load necessary libraries 
library(dplyr)
library(ggplot2)
library(lubridate)
library(tidyr)
library(stringr)

# ----
# 1. Compare parameters ----
# ----


# ---
# Input data formatting
# ---

# File with set parameters
setParam <- read.csv("../Files/Parameters/octoPusParameters.csv")

options(scipen = 999) # For better data visualization 

# Only target paramenters
setParam <- setParam[72:84,] # Remove parameters 
setParam <- setParam[,2:5]
# Optimization parameters directory
OptParamDir <- "../bin/Debug/net8.0/calibratedParametersPhenology/"


# Read files
listFiles <- list.files(path = OptParamDir, pattern = "\\.csv$", full.names = TRUE)
files <- list()

# Populate file list
for (f in listFiles) {
  
  df <- read.csv(f)
  
  # Create site column from paths
  df$site <- sub("^calibParam_|\\.csv$", "", basename(f))
  files[[basename(f)]] <-df
}

# Export file from the list
allFiles <- do.call(rbind, files)
# Remove row-name
row.names(allFiles) <- NULL
# Remove df
rm(df)

# Rename columns
optParam <- allFiles|>
  rename(value_opt = value,
         parameter = param)



# ---
# Parameter comparison between set and optimized values
# ---

# Merge setParam and optParam
df <- merge(setParam, optParam, by = "parameter")|>
  relocate(site, .before = parameter)

# Parameter comparison for each site
stats <- df |>
  mutate(
    # Direction of change (+ increased, - decreased, 0 unchanged)
    diff = case_when(
      value_opt > value ~ "+",
      value_opt < value ~ "-",
      TRUE ~ "0"
    ),
    # Absolute difference between optimized and set parameter values
    abs_diff = abs(value_opt - value),
    
    # Relative error (%) between original and optimized parameter values
    rel_error = round(abs(value_opt - value) / abs(value) * 100,2),
    
    # Whether optimized value is within the predefined parameter range
    in_range = value_opt >= min & value_opt <= max,
    
    # Normalized position of optimized value within the allowed range [0–1]
    # 0 = close to min, 0.5 = central, 1 = close to max
    position_norm = (value_opt - min) / (max - min),
    
    # Qualitative label for range position (useful for summary plots)
    position = case_when(
      position_norm < 0.33 ~ "closer to min",
      position_norm >= 0.33 & position_norm <= 0.66 ~ "central",
      position_norm > 0.66 ~ "closer to max",
      TRUE ~ NA_character_
    )
  )

# Summary by parameter (averaged across all sites)
stats_param <- df |>
  mutate(
    # Check if optimized value is within parameter limits
    in_range = value_opt >= min & value_opt <= max,
    # Normalized position of optimized value within range [0–1]
    position_norm = (value_opt - min) / (max - min),
    # Relative error (%) between original and optimized parameter values
    rel_error = round(abs(value_opt - value) / abs(value) * 100,2)
  ) |>
  group_by(parameter) |>
  summarise(
    # Mean Absolute Error: average magnitude of change between set and optimized values
    MAE  = mean(abs(value_opt - value), na.rm = TRUE),
    # Root Mean Square Error: average error weighted toward larger deviations
    RMSE = sqrt(mean((value_opt - value)^2, na.rm = TRUE)),
    # Percentage of optimized values within the allowed range
    perc_in_range = mean(in_range, na.rm = TRUE) * 100,
    # Average normalized position of optimized values (0=min, 0.5=center, 1=max)
    position = mean(position_norm, na.rm = TRUE),
    # Mean relative error (%): average percentage change from original to optimized values
    mean_rel_error = mean(rel_error, na.rm = TRUE)
    
  )


# 
# Comparison of optimized and default parameter values across sites
# 

# Set how many sites to display per plot
sites_per_plot <- 6
# List of all sites 
all_sites <- unique(df$site)
# Split sites into groups of N sites each
site_groups <- split(all_sites, ceiling(seq_along(all_sites) / sites_per_plot))
# Function to create the plot (reused twice) 
make_plot <- function(data_subset, title_suffix = "") {
  ggplot(data_subset, aes(x = parameter)) +
    
    # Grey vertical line for the [min, max] range
    geom_segment(aes(y = min, yend = max, xend = parameter),
                 color = "grey75", linewidth = 1.2) +
    # Blue point = set (default) value
    geom_point(aes(y = value, color = "Set value"), size = 2.8) +
    # Red point = optimized value
    geom_point(aes(y = value_opt, color = "Optimized value"),
               size = 2.8, shape = 17) +
    
    geom_text(aes(y = (value + value_opt) / 2,
                  label = round(value_opt - value, 1)),
              color = "black", size = 2.5, vjust = -1, hjust= 0.2)+
    
    # One panel per site
    facet_wrap(~ site, scales = "free_y") +
    # Manual color scheme
    scale_color_manual(values = c("Set value" = "#1f77b4",
                                  "Optimized value" = "#d62728")) +
    labs(
      title = paste("Parameter comparison", title_suffix),
      x = "Parameter",
      y = "Value",
      color = ""
    ) +
    theme_minimal(base_size = 12) +
    theme(
      strip.text = element_text(face = "bold"),
      axis.text.x = element_text(angle = 45, hjust = 1),
      legend.position = "bottom",
      plot.title = element_text(face = "bold", size = 13)
    )
}

# Loop over site groups
for (i in seq_along(site_groups)) {
  
  sites_subset <- site_groups[[i]]
  
  # Filter data for current group
  df_sub <- df |> filter(site %in% sites_subset)
  
  # Separate datasets
  df_allParam <- df_sub |> filter(parameter != "CycleLength")
  df_cycleLenght <- df_sub |> filter(parameter == "CycleLength")
  
  # Plot 1: all parameters except CycleLength
  p1 <- make_plot(df_allParam, "(excluding CycleLength)")
  print(p1)
  readline(prompt = "Press [Enter] to continue to CycleLength plot...")
  
  # Plot 2: CycleLength only
  if (nrow(df_cycleLenght) > 0) {
    
    p2 <- make_plot(df_cycleLenght, "(CycleLength only)")
    print(p2)
  }
  
  readline(prompt = "Press [Enter] to continue to the next group...")
}


#
# Global comparison of optimized and default parameter 
#

# Split the dataset
allParams <- df |> filter(parameter != "CycleLength")

cycleLenght <- df |> filter(parameter == "CycleLength")

# Plot function 
plot_param_values <- function(data, title_text) {
  ggplot(data, aes(x = parameter)) +
    # Range line [min, max] (vertical range)
    geom_segment(aes(y = min, yend = max, xend = parameter),
                 color = "grey74", linewidth = 1.2) +
    # Blue points = set (default) values for all sites
    geom_jitter(aes(y = value, color = "Set value"),
                width = 0.1, height = 0, size = 2, alpha = 0.7) +
    # Red triangles = optimized values for all sites
    geom_jitter(aes(y = value_opt, color = "Optimized value"),
                width = 0.1, height = 0, size = 2, shape = 17, alpha = 0.7) +
    # Manual color scheme
    scale_color_manual(values = c("Set value" = "#1f77b4",
                                  "Optimized value" = "#d62728")) +
    labs(
      title = title_text,
      x = "Parameter",
      y = "Parameter value",
      color = NULL
    ) +
    theme_minimal(base_size = 12) +
    theme(
      axis.text.x = element_text(angle = 45, hjust = 1),
      plot.title = element_text(face = "bold", size = 13),
      legend.position = "bottom"
    )
}

# Plot 1: all parameters except cycleLength 
p1 <- plot_param_values(
  allParams,
  "Global visualization of parameter variability (excluding CycleLength)"
)
print(p1)

# Plot 2: cycleLength only 
p2 <- plot_param_values(
  cycleLenght,
  "Global visualization — CycleLength only"
)
print(p2)

#
# 2. Plot BBCH from output file ----
# 

# ---
# Input data formatting
# ---

# octoPus output directory
OutDir <- "../bin/Debug/net8.0/outputs/diseaseModels"


# Read files
listFiles <- list.files(path = OutDir, pattern = "\\.csv$", full.names = TRUE)
files <- list()

# Populate file list
for (f in listFiles) {
  df <- read.csv(f, row.names = NULL)
  # if columns are "shiftated"
  colnames(df) <- colnames(df)[2:ncol(df)]
  # remove last empty column
  df <- df[1:(ncol(df)-1)]
  files[[basename(f)]] <- df
}

# Export file from the list
allFiles <- do.call(rbind, files)
# Remove row-name
row.names(allFiles) <- NULL
# Remove df
rm(df)

# Dataset formatting
outOct <- allFiles|>
  #leave only target column
  select(site,Date,chillState,forcingState,cycleCompletion,bbchCode,bbchRef, plantSusceptibility)
#
# Plot results
#

# Convert the Date column to Date class
outOct <- outOct |>
  mutate(Date = as.POSIXct(Date, format = "%m/%d/%Y %I:%M:%S %p")) |>
  mutate(Date = as.Date(Date)) 


# Split sites into groups of 12
sites_per_plot <- 6
all_sites <- unique(outOct$site)
site_groups <- split(all_sites, ceiling(seq_along(all_sites) / sites_per_plot))

# Loop over groups
for (i in seq_along(site_groups)) {
  
  sites_subset <- site_groups[[i]]
  df_sub <- outOct |> filter(site %in% sites_subset)
  
  p <- ggplot(df_sub, aes(x = Date)) +
    # Blue line = simulated BBCH
    geom_line(aes(y = bbchCode, group = site),
              color = "#1f77b4", linewidth = 0.8) +
    
    # Red triangles = optimized/reference BBCH
    geom_point(aes(y = bbchRef),
               color = "#d62728", size = 2, shape = 17, na.rm = TRUE) +
    
    # Dashed line connecting optimized points
    geom_line(aes(y = bbchRef),
              color = "#d62728", linewidth = 0.6, linetype = "dashed", na.rm = TRUE) +
    
    # Vertical dotted lines at optimized BBCH dates
    geom_vline(aes(xintercept = Date),
               data = df_sub |> filter(!is.na(bbchRef)),
               color = "grey70", linetype = "dotted", linewidth = 0.7) +
    
    # Date axis formatting: show one label every 5 months
    scale_x_date(
      date_breaks = "5 months",
      date_labels = "%b %Y"
    ) +
    
    labs(
      x = "Date",
      y = "BBCH code",
      caption = "Blue = simulated BBCH; Red triangles/dashed = optimized reference"
    ) +
    geom_text(aes(y = bbchRef, label = bbchRef),
              color = "#d62728",
              size = 4,
              vjust = -1,
              hjust = - 1)+
    
    facet_wrap(~ site, scales = "free_x") +
    theme_minimal(base_size = 11) +
    theme(
      strip.text = element_text(face = "bold", size = 10),
      axis.text.x = element_text(angle = 45, hjust = 1, size = 8),
      plot.title = element_text(face = "bold", size = 13),
      panel.grid.minor = element_blank(),
      legend.position = "none"
    )
  
  print(p)
  readline(prompt = paste0("Press [Enter] to continue to next group (", i, "/", length(site_groups), ")..."))
}








