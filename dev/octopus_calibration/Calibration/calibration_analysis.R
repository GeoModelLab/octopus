rm(list=ls())

library(data.table)
library(dplyr)
library(tidyr)
library(sf)
library(giscoR)
library(ggplot2)
library(viridis)
library(scales)
library(data.table)
library(stringr)



# Set the working directory to the script's location
setwd(dirname(rstudioapi::getActiveDocumentContext()$path))

# ---
# Output simulation ---
# ---

# List all calibration files
calibrationFiles <- list.files(
  "..//bin//Debug//net8.0//outputs//diseaseModels",
  full.names = TRUE
)

df<-do.call(rbind,lapply(calibrationFiles,fread))

# Read each file, add Model column, and row-bind them
df <- do.call(rbind, lapply(calibrationFiles, function(f) {
  
  # Read file (change to readRDS/read.table/etc. if needed)
  dat <- fread(f)
  
  # Extract file name without path
  fname <- basename(f)
  
  # Split by "_" and get first piece
  model_name <- strsplit(fname, "_")[[1]][1]
  
  # Add column
  dat$Model <- model_name
  
  dat
}))

df_long<-df |> 
  select(1,2,16,17,27:34) |> 
  mutate(date = as.Date(Date,format='%m/%d/%Y'),
         year = year(date),
         doy=yday(date)) |>
  pivot_longer(cols=c(5:12),names_to='Model_name',values_to="Onset") |> 
  filter(Onset==1) |> 
  group_by(site,year,Model_name) |> 
  slice_head() |> 
  select(site,year,doy,bbchPhase,bbchRef) |> 
  rename(Onset=doy,
         Model=Model_name) |> 
  mutate(site = sub("\\.csv$", "", site))



df_reference <- fread("..//files//Reference//reference_file.csv") |> 
  mutate(onsetDate = as.Date(onsetDate,format = "%m/%d/%Y")) |> 
  mutate(year = year(onsetDate),
         doy = yday(onsetDate)) |> 
  select(-onsetDate) |> 
  rename(Onset=doy) |> 
  mutate(Model='Reference',
         bbchPhase=NA,
         bbchRef=NA)

head(df_reference)
df_all<-rbind(df_long,df_reference)

ggplot(df_all) + 
  geom_col(aes(x=Model,y=Onset,fill=factor(year)),position=position_dodge())+
  facet_wrap(~site)+
  theme(axis.text.x = element_text(angle=90))

# Calculate differences in onset (between octopus models and Refenrence)
df_diff <- df_all|>
  group_by(site, year)|>
  mutate(Onset_ref = Onset[match("Reference", Model)],
         diff = Onset - Onset_ref)|>
  filter(str_starts(Model, "ons"),
         !is.na(Onset_ref))|>
  ungroup()

# Group by model (omitting sites)
df_diff_model_year <- df_diff|>
  group_by(Model, year)|>
  summarise(overall_diff = round(mean(diff, na.rm = T)),
            mean_doy_model = round(mean(Onset, na.rm = T)),
            mean_doy_ref = round(mean(Onset_ref, na.rm = T)))
# Transform in long format
df_bar_year <- df_diff_model_year |>
  pivot_longer(
    cols = c(mean_doy_model, mean_doy_ref),
    names_to = "DOY_type",
    values_to = "mean_DOY"
  ) |>
  mutate(
    DOY_type = ifelse(DOY_type == "mean_doy_model", "Model", "Reference")
  )

# Histogram with differences
year_diff_p <- ggplot(df_bar_year, aes(x = factor(year), y = mean_DOY, fill = DOY_type)) +
  geom_col(position = position_dodge(width = 0.8)) +
  facet_wrap(~ Model) +
  theme_bw() +
  theme(
    axis.text.x = element_text(angle = 45, hjust = 1)
  ) +
  labs(
    x = "year",
    y = "doy",
    title = "doy differences (model - ref) by year for each model"
  )+
  geom_text(
    data = df_bar_year|> filter(DOY_type == "Model"),
    aes(
    x = factor(year),
    y = mean_DOY,
    label = overall_diff),
    position = position_dodge(width = 0.8),
    vjust = 2,
    size = 4
  )
year_diff_p
ggsave("plot/Year_diff.jpg", year_diff_p, width = 10, height = 7)


# Group by model (omitting year)
df_diff_model_site <- df_diff|>
  group_by(Model, site)|>
  summarise(overall_diff = round(mean(diff, na.rm = T)),
            mean_doy_model = round(mean(Onset, na.rm = T)),
            mean_doy_ref = round(mean(Onset_ref, na.rm = T)))
# Transform in long format
df_bar_site <- df_diff_model_site |>
  pivot_longer(
    cols = c(mean_doy_model, mean_doy_ref),
    names_to = "DOY_type",
    values_to = "mean_DOY"
  ) |>
  mutate(
    DOY_type = ifelse(DOY_type == "mean_doy_model", "Model", "Reference")
  )

# Histogram with differences

# select some sites
sites_to_plot <- unique(df_bar_site$site)[50:69]
df_bar_site_filtered <- df_bar_site|>
  filter(site %in% sites_to_plot)

site_diff_p <- ggplot(df_bar_site_filtered, aes(x = factor(site), y = mean_DOY, fill = DOY_type)) +
  geom_col(position = position_dodge(width = 0.8)) +
  facet_wrap(~ Model) +
  theme_bw() +
  theme(
    axis.text.x = element_text(angle = 90, hjust = 1)
  ) +
  labs(
    x = "site",
    y = "doy",
    title = "Doy diff (model - ref) by site  for each model"
  )+
  geom_text(
    data = df_bar_site_filtered|> filter(DOY_type == "Model"),
    aes(
      x = factor(site),
      y = mean_DOY,
      label = overall_diff),
    position = position_dodge(width = 0.8),
    vjust = 2,
    size = 2.5
  )
site_diff_p
ggsave("plot/Sites_diff_50_69.jpg", site_diff_p, width = 10, height = 7)


# Differences by year and site
years <- sort(unique(df_diff$year))

for (yr in years) {
  
  df_yr <- df_diff |>
    filter(year == yr)
  
  p <- ggplot(df_yr, aes(x = site, y = diff)) +
    geom_col(fill = "navy", size = 2) +
    geom_hline(yintercept = 0, linetype = "dashed") +
    facet_wrap(~ Model, ncol = 3) +
    theme_bw() +
    theme(
      axis.text.x = element_text(angle = 90, hjust = 1)
    ) +
    labs(
      title = paste("DOY difference (Model – Reference) for year", yr),
      x = "Site",
      y = "Diff (days)"
    )
  
  print(p)
  
  ggsave(
    filename = paste0("plot/diff_by_site_year_", yr, ".png"),
    plot = p,
    width = 12,
    height = 8
  )
}


# un grafico per sito
# asse x: modello + reference
# asse y: valori doy
df_site_plot <- df_diff |>
  select(site, year, Model, Onset, Onset_ref) |>
  mutate(Reference = Onset_ref) |>
  pivot_longer(
    cols = c(Onset, Reference),
    names_to = "Type",
    values_to = "DOY"
  ) |>
  mutate(
    Type = ifelse(Type == "Onset", Model, "Reference"),
    Model_label = Type
  ) |>
  select(site, year, Model_label, DOY)


#heat map
df_heat <- df_diff |> 
  mutate(Model_factor = factor(Model),
         site_factor = factor(site))

heat_map <- ggplot(df_heat, aes(x = Model_factor, y = site_factor, fill = diff)) +
  geom_tile() +
  scale_fill_gradient2(
    low = "darkgreen", mid = "white", high = "firebrick",
    midpoint = 0, name = "DOY diff"
  ) +
  theme_bw() +
  labs(
    x = "Model",
    y = "Site",
    title = "Differenza (doy simulato – doy osservati)"
  ) +
  theme(
    axis.text.x = element_text(angle = 90, hjust = 1, face = "bold", size = 10),
    axis.text = element_text(face = "bold", size = 8)
  )
heat_map
ggsave("plot/heatmap_diff.jpg",heat_map,width = 10, height = 8)

#boxplot per modello
box_plot_p <- ggplot(df_diff, aes(x = Model, y = diff)) +
  geom_boxplot(fill = "lightgray") +
  geom_hline(yintercept = 0, linetype = "dashed") +
  theme_bw() +
  labs(
    x = "Model",
    y = "DOY difference",
    title = "Distribution of differences across sites"
  ) +
  theme(axis.text.x = element_text(angle = 90))
box_plot_p
ggsave("plot/boxplot_diff.jpg",box_plot_p,width = 10, height = 6)

# Overall differences (omitting year and site)
df_summary <- df_diff |>
  group_by(Model) |>
  summarise(
    overall_diff = round(mean(diff, na.rm = TRUE))
  ) |>
  ungroup()

overal_diff_p <- ggplot(df_summary, aes(x = overall_diff, y = Model)) +
  geom_point(size = 4, color = "red4") +              # punto
  geom_segment(aes(x = 0, xend = overall_diff, 
                   y = Model, yend = Model), 
               color = "grey50", linewidth = 1) +        # linea
  theme_bw() +
  labs(
    title = "Mean global difference (Model – Reference)",
    x = "doy diff",
    y = "Model"
  )
overal_diff_p
ggsave("plot/Overall diff.jpg", overal_diff_p, width = 8, height = 6)


# ---
# Param distribution ---
# ---

# File with set parameters
setParam <- read.csv("../Files/Parameters/octoPusParameters.csv")

options(scipen = 999) # For better data visualization 

# Only target paramenters
setParam <- setParam[1:68,] # Remove parameters 
setParam <- setParam[,1:5]

setParam <- setParam |>
  rename(param = parameter,
         model = class)|>
  filter(param != "bbchThreshold")
  



# Optimization parameters directory
OptParamDir <- "../bin/Debug/net8.0/calibratedParameters/"


# Read files
listFiles <- list.files(path = OptParamDir, pattern = "\\.csv$", full.names = TRUE)
files <- list()

# Populate file list
for (f in listFiles) {
  
  df <- read.csv(f)
  
  # Create site column from paths
  df$model <- sub("^calibParam_", "", sub("\\.csv$", "", basename(f)))
  files[[basename(f)]] <-df
}

# Export file from the list
allFiles <- do.call(rbind, files)
# Remove row-name
row.names(allFiles) <- NULL
# Remove df
rm(df)

optParam <- allFiles|>
  mutate(value_opt = value)|>
  select(-value)|>
  mutate(value_opt = round(value_opt,3))

# Merge setParam and optParam
df <- optParam|>
  left_join(setParam, by = c("param", "model"))


# Lista modelli
models <- unique(df$model)

for (m in models) {
  
  df_m <- df |> filter(model == m)
  df_m$x <- 1   # asse X fittizio perché non hai "site"
  
  p <- ggplot(df_m, aes(x = x, y = value_opt)) +
    geom_hline(aes(yintercept = min), 
               color = "IndianRed3", linetype = "dashed", size = 1) +
    geom_hline(aes(yintercept = max),
               color = "IndianRed3", linetype = "dashed", size = 1) +
    geom_point(color = "DarkOliveGreen4", size = 3) +
    facet_wrap(~ param, scales = "free_y") +
    theme_bw() +
    theme(
      axis.text.x = element_blank(),
      axis.ticks.x = element_blank(),
      strip.text = element_text(face = "bold")
    ) +
    labs(
      x = "",
      y = "Value",
      title = paste("Optimized parameters distribution —", m)
    )
  
  print(p)
  
  ggsave(paste("plot/param_distr_",m, ".jpeg"),p, width = 10, height = 5)
}

# Ma queste differenze sono legate all'incubazione oppure alla comparsa dell'infezione????

#1. calcola differenza tra prima infezione e onset simulati di ciascun modello
#2. usando un valore medio di incubazione, come proxy di stima infezione a partire da
# onset reali, calcolati la differenza tra le infezioni simulate e e quelle dell'onset reale

# 1. calcola differenza tra infezione e onset simulati
df_onset <- df |>
  select(1, 2,15, 27:34) |>
  mutate(
    date = as.Date(Date, format = "%m/%d/%Y"),
    year = year(date),
    doy_onset  = yday(date)) |>
  filter(bbchCode >=10)|>
  select(-bbchCode)|>
  pivot_longer(
    cols = 3:10,
    names_to  = "Model",
    values_to = "Onset"
  ) |>
  group_by(site, year, Model) |>
  filter(Onset > 0) |>
  slice_head() |>
  mutate(site = sub("\\.csv$", "", site)) |>
  ungroup() |>
  select(site, year, Model, doy_onset)|>
  mutate(Model = sub("^ons","", Model))


df_inf <- df |>
  mutate(
    date = as.Date(Date, format = "%m/%d/%Y"),
    year = year(date),
    doy_inf  = yday(date)
  ) |>
  select(site, date, bbchCode, year, doy_inf, pressureRule310:pressureLaore) |> 
  filter(bbchCode >=10)|>
  select(-bbchCode)|>
  pivot_longer(
    cols = pressureRule310:pressureLaore,
    names_to  = "Model",
    values_to = "Infect"
  ) |>
  group_by(site, year, Model) |>
  filter(Infect > 0) |>
  slice_head() |>
  ungroup() |>
  mutate(site = sub("\\.csv$", "", site)) |>
  select(site, year, Model, doy_inf)|>
  mutate(Model = sub("^pressure","", Model))

df_merge <- df_onset|>
  left_join(df_inf, by = c("site","year","Model"))|>
  #add difference in days between infection and onset simulated (incubation simulated)
  mutate(incubSim = doy_onset - doy_inf)

# verify whim model has a long incubation
incub_long <- df_merge|>
  filter(incubSim <=20)
