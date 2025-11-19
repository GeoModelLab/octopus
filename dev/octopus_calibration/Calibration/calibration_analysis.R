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

sf::sf_use_s2(TRUE)


# Set the working directory to the script's location
setwd(dirname(rstudioapi::getActiveDocumentContext()$path))

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
