#---
# Set up----
#---
# Set working directory 
setwd(dirname(rstudioapi::getActiveDocumentContext()$path))
thisDir<-getwd()
# Load necessary libraries 
library(dplyr)
library(ggplot2)
library(lubridate)
library(tidyr)
library(sf)
library(factoextra)
library(FactoMineR)
library(stringr)


# ---
# Data Import and Preparation----
# ---
# Import Octopus sites' weather data
Data <- read.csv("Weather_octopusSites.csv")
str(Data)
# Convert the 'Date' column to a proper Date format
Data <- Data|> 
  mutate(Date = as.Date(Date))|>
  # Standardize the strings in the 'Location' column (first letter uppercase, rest lowercase)
  mutate(
    Location = str_trim(Location),   # Remove spaces
    Location = paste0(
    str_to_upper(str_sub(Location, 1, 1)), # Convert the first character to uppercase
    str_to_lower(str_sub(Location, 2, str_length(Location))) # Convert the rest of the string to lowercase
  ))
         
# Add a 'Location ID' column for unique site identification
# Define a lookup table for province codes
unique(Data$Province)
provinceCodes <- tibble::tribble(
  ~Province,            ~ProvCode,
  "Alessandria",         "AL",
  "Ancona",              "AN",
  "Ascoli Piceno",       "AP",
  "Asti",                "AT",
  "Cuneo",               "CN",
  "Arezzo",              "AR",
  "Avellino",            "AV",
  "Bergamo",             "BG",
  "Brescia",             "BS",
  "Ravenna",             "RA",
  "Chieti",              "CH",
  "Pordenone",           "PN",
  "Udine",               "UD",
  "Gorizia",             "GO",
  "Genova",              "GE",
  "Imperia",             "IM",
  "Macerata",            "MC",
  "Matera",              "MT",
  "Bologna",             "BO",
  "Novara",              "NO",
  "Nuoro",               "NU",
  "Oristano",            "OR",
  "Sud Sardegna",        "SU",
  "Pesaro e Urbino",     "PU",
  "Potenza",             "PZ",
  "Pescara",             "PE",
  "Verona",              "VR",
  "Sondrio",             "SO",
  "La Spezia",           "SP",
  "Savona",              "SV",
  "Treviso",             "TV",
  "Benevento",           "BN",
  "Bari",                "BA",
  "Vicenza",             "VI"
)
# Join the province codes
Data <- Data |>
  left_join(provinceCodes, by = c("Province" = "Province"))|>
  relocate(ProvCode, .after = Province)|>
  # Create a unique 'LocationID' by concatenating region, province code, and location
  mutate(
    RegionAbbr = str_to_upper(str_sub(Region, 1, 3)),
    LocationID = str_c(RegionAbbr, ProvCode, Location, sep = "-")
  )|>
  relocate(LocationID, .after = ProvCode)|>
  select(-RegionAbbr) |> # Remove the temporary 'RegionAbbr' column
  # Append a numeric index to each Location with multiple SiteIDs
  group_by(LocationID) |>
  mutate(
    SiteIndex = dense_rank(SiteID),
    Location_dist = case_when(
      n_distinct(SiteID) > 1 ~ paste0(LocationID, "_", SiteIndex),
      TRUE ~ LocationID)) |>
  ungroup()|>
  select(-LocationID, SiteIndex)|>
  rename(LocationID = Location_dist)|>
  relocate(LocationID, .after = ProvCode)

rm(provinceCodes)

# ---
# Estimate Weather Parameters----
# ---
# NOTE: The weather data lacks relative humidity, which must be estimated.
# ---
# Function to calculate dew point temperature using an empirical formula
dewPointTemperature <- function(tmax, tmin){
  dewPoint <- 0.38 * tmax  - 0.018 * tmax^2 + 1.4 * tmin - 5
  return(dewPoint)
}
# Function to calculate relative humidity (RH) from temperature and dew point
relativeHumidity <- function(tmax, tmin, dewpoint){
  
  #daily avg temperature
  avgT  <- (tmax + tmin) / 2
  #saturated vapor pressure (es)
  es <- 0.61121 * exp((17.502 * avgT) / (240.97 + avgT))
  #actual vapore pressure (ea)
  ea <- 0.61121 * exp((17.502 * dewpoint) / (240.97 + dewpoint))
  #rh
  rh <- (ea / es) * 100
  #range of 0-100%
  rh <- pmin(pmax(rh, 0), 100)
  
  return(rh)
}
# Add estimated dew point and relative humidity columns 
Data <- Data|>
  mutate(dewpoint = dewPointTemperature(tmax,tmin),
         rh = relativeHumidity(tmax,tmin, dewpoint))
head(Data)

# ---
# Seasonal data aggregation----
# ---
# Function to assign a season to each date
getSeason <- function(input.date){
  numeric.date <- 100*month(input.date)+day(input.date)
  # input Seasons upper limits in the form MMDD in the "break =" option:
  cuts <- base::cut(numeric.date, breaks = c(0,319,0620,0921,1220,1231)) 
  # rename the resulting groups (could've been done within cut(...levels=) if "Winter" wasn't double
  levels(cuts) <- c("Winter","Spring","Summer","Fall","Winter")
  return(cuts)
}

# Add a 'season' column 
Data <-Data |>
  mutate(season = getSeason(Date))

# # Aggregate data by season, calculating mean (mu) and coefficient of variation (cv) for key variables
Seasonal <- Data|>
   group_by(SiteID,Region,Province,ProvCode,LocationID,lon,lat,season)|>
     summarise(
       tx_mu = mean(tmax, na.rm = TRUE),
       #tx_cv = sd(tmax, na.rm = TRUE)/tx_mu*100,
       tn_mu = mean(tmin, na.rm = TRUE),
       #tn_cv = sd(tmin, na.rm = TRUE)/tn_mu*100,
       #rh_mu = mean(rh, na.rm = TRUE),
       #rh_cv = sd(rh, na.rm = TRUE)/rh_mu*100,
       prc_mu = mean(prec, na.rm = TRUE),
       #prc_cv = sd(prec, na.rm = TRUE)/prc_mu*100,
       .groups = "drop"
     )
# Remap the season names to two-letter abbreviations
 seasNames <- c("su","wi","fa","sp")
Seasonal$season<-plyr::revalue(Seasonal$season,c("Summer" = seasNames[1],
                                                  "Winter" = seasNames[2],
                                                  "Fall" = seasNames[3],
                                                  "Spring" = seasNames[4]))


# ---
# Principal Components analysis----
# ---

# Reshape the seasonal data from long to wide format for PCA
PcaDfSeason <- Seasonal |>
  pivot_wider(names_from = season,
              values_from = c(tx_mu, 
                              #tx_cv, 
                              tn_mu, 
                              #tn_cv, 
                              #rh_mu, 
                              #rh_cv, 
                              prc_mu)) |>
                              #prc_cv))
                              
  drop_na()

# Verify Multicollinearity among variables
corr <- GGally::ggpairs(PcaDfSeason[, -c(1:7)])
print(corr)
# Save plot
ggsave("plot/VarCorrelation.png", corr, width = 7, height = 6, dpi = 350)
# RESULT: high general correlation between climatic variables


# Select variables for PCA, excluding non-numeric and metadata columns
pca_data <- PcaDfSeason |> select(-c(1,4,6,7)) # Remove any rows with missing values
# Define the indices of the qualitative supplementary variables
quali_idx <- c(1,2,3)

# Perform PCA on the standardized data
pcaSeason <- PCA(pca_data, quali.sup = quali_idx, scale.unit = TRUE, # Standardize variables to unit variance
                 graph = FALSE) # Suppress default plots


#Correlation between climate variables and PC (1,2)
var <-pca_data[, -c(1:3)]
cor(var, pcaSeason$ind$coord[, 1:2])
GGally::ggcorr(cbind(var, pcaSeason$ind$coord[, 1:2]), label = TRUE, cex = 2.5)

# ---
# Visualization of Principal component (PC1, PC2 only) with location information
# ---

# Biplot (base)
# Recover explained variance
eig <- pcaSeason$eig

bp <- fviz_pca_var(
  pcaSeason,
  axes = c(1, 2),
  col.var = "cos2",
  gradient.cols = c("SlateGray3", "blue", "red"),
  repel = TRUE,
  arrowsize = 1.2,
  labelsize = 5,
  title = paste0("Biplot PC1 (", round(eig[1,2],1), "%) vs PC2 (", round(eig[2,2],1), "%)")
) +
  theme_minimal(base_size = 14) +
  theme(
    plot.title = element_text(hjust = 0.5, face = "bold"),
    legend.position = "right"
  )

print(bp)
# Save plot
ggsave("plot/Biplot_PC1_PC2NORH.png", bp, width = 7, height = 6, dpi = 350)

# Provinces distribution in the same space
# Provinces coords for biplot
Provinces <- data.frame(
  PC1 = pcaSeason$quali.sup$coord[c(14:47), 1],
  PC2 = pcaSeason$quali.sup$coord[c(14:47), 2])
# Normalize [-1, 1]
Provinces_scaled <- Provinces
scale_factor <- max(abs(c(Provinces$PC1, Provinces$PC2)))
Provinces_scaled$PC1 <- Provinces$PC1 / scale_factor
Provinces_scaled$PC2 <- Provinces$PC2 / scale_factor

#Biplot provinces
biplot_prov <- ggplot(Provinces_scaled, aes(x = PC1, y = PC2)) +
  # Assi centrali
  geom_hline(yintercept = 0, color = "grey20", linewidth = 0.8) +
  geom_vline(xintercept = 0, color = "grey20", linewidth = 0.8) +

  # Province (punti + etichette)
  geom_point(color = "VioletRed4", alpha = 0.3, size = 3) +
  ggrepel::geom_text_repel(
    aes(label = rownames(Provinces_scaled)),
    color = "VioletRed4",
    size = 4,
    max.overlaps = 50,
    alpha = 0.6
  ) +
  coord_equal(xlim = c(-1, 1), ylim = c(-1, 1)) +
  scale_x_continuous(breaks = seq(-1, 1, 0.5), limits = c(-1, 1)) +
  scale_y_continuous(breaks = seq(-1, 1, 0.5), limits = c(-1, 1)) +
  theme_minimal(base_size = 14) +
  theme(
    plot.title = element_text(hjust = 0.5, face = "bold"),
    panel.grid = element_line(color = "grey90"),
    axis.title = element_text(face = "bold")
  ) +
  labs(
    title = "Provinces distribution on PC1 and PC2 (scaled)",
    x = paste0("PC1 (", round(eig[1,2], 1), "%)"),
    y = paste0("PC2 (", round(eig[2,2], 1), "%)")
  )
biplot_prov
# Save plot
ggsave("plot/Biplot_PC1_PC2ProvincesNORH.png", biplot_prov, width = 7, height = 6, dpi = 350)



# Biplot (var and quali.index in the same space)
# biplot <- fviz_pca_biplot(
#   pcaSeason,
#   label = "var",                    
#   repel = TRUE,
#   col.ind = "transparent",          
#   col.var = "SteelBlue",              
#   geom.var = c("arrow", "text"),
#   arrowsize = 1,                 
#   labelsize = 6,                   
#   title = "Biplot PCA - Season"
# ) +
#   theme_minimal(base_size = 14) +
#   theme(
#     plot.title = element_text(face = "bold", hjust = 0.5, size = 16),
#     panel.grid = element_line(color = "white"),
#     axis.title = element_text(size = 15, face = "bold"),
#     axis.text = element_text(size = 13)
#   )
# 
# # Province coords for biplot
# Provinces <- data.frame(
#   PC1 = pcaSeason$quali.sup$coord[c(13:47), 1],
#   PC2 = pcaSeason$quali.sup$coord[c(13:47), 2]
# )
# 
# # Add provinces
# biplot <- fviz_add(
#   biplot,
#   Provinces,
#   geom = "text",
#   color = scales::alpha("Coral3", 0.4),
#   labelsize = 5,
#   repel = TRUE
# )
# 
# 
# print(biplot)
# 
# Save
# ggsave("plot/Biplot_PC1PC2_Prov.png", biplot, width = 10, height = 8, dpi = 400)
# 
# Remove object
# rm(biplot)


# ---
# Visualization of Principal component (3 PC)
# ---

# Recover explained variance
eig <- pcaSeason$eig
# PC1 vs PC2
p12 <- fviz_pca_var(
  pcaSeason,
  axes = c(1, 2),
  col.var = "cos2",                       # Quality of representation
  gradient.cols = c("white", "blue", "red"),
  repel = TRUE,                           
  arrowsize = 0.8,
  labelsize = 4,
  title = paste0("Biplot PC1 (", round(eig[1,2],1), "%) vs PC2 (", round(eig[2,2],1), "%)")
) +
  theme_minimal(base_size = 14) +
  theme(
    plot.title = element_text(hjust = 0.5, face = "bold"),
    legend.position = "right"
  )
# Save plot
ggsave("plot/Biplot_PC1_PC2.png", p12, width = 7, height = 6, dpi = 350)

# PC1 vs PC3
p13 <- fviz_pca_var(
  pcaSeason,
  axes = c(1, 3),
  col.var = "cos2",
  gradient.cols = c("white", "blue", "red"),
  repel = TRUE,
  arrowsize = 0.8,
  labelsize = 4,
  title = paste0("Biplot PC1 (", round(eig[1,2],1), "%) vs PC3 (", round(eig[3,2],1), "%)")
) +
  theme_minimal(base_size = 14) +
  theme(
    plot.title = element_text(hjust = 0.5, face = "bold"),
    legend.position = "right"
  )
ggsave("plot/Biplot_PC1_PC3.png", p13, width = 7, height = 6, dpi = 350)

#PC2 vs PC3
p23 <- fviz_pca_var(
  pcaSeason,
  axes = c(2, 3),
  col.var = "cos2",
  gradient.cols = c("white", "blue", "red"),
  repel = TRUE,
  arrowsize = 0.8,
  labelsize = 4,
  title = paste0("Biplot PC2 (", round(eig[2,2],1), "%) vs PC3 (", round(eig[3,2],1), "%)")
) +
  theme_minimal(base_size = 14) +
  theme(
    plot.title = element_text(hjust = 0.5, face = "bold"),
    legend.position = "right"
  )
ggsave("plot/Biplot_PC2_PC3.png", p23, width = 7, height = 6, dpi = 350)

#remove objects
rm(p12)
rm(p13)
rm(p23)

# ---
# PC assessment for Cluster analysis
# ---

# Visualize the eigenvalues (scree plot) to determine the number of principal components to retain
fviz_screeplot(pcaSeason, addlabels = TRUE)


# Summary of the PCA results
summary(pcaSeason)

# Get and print the loadings
loadings <- pcaSeason$var$coord
print(round(loadings, 3))


# Visualize the optimal number of clusters (on PC1 and PC2 Only)
pca_coords <- pcaSeason$ind$coord[, 1:2] 
pca_coords
#PC2, PC2, PC3
#pca_coords <- pcaSeason$ind$coord[, 1:3] 
#pca_coords

fviz_nbclust(pca_coords, kmeans, method = "wss")     #Elbow method
 
fviz_nbclust(pca_coords, kmeans, method = "silhouette") #Silhouette method
 

 # ---
 # HCPC----
 # (Hierarchical Clustering on Principal Components)
 # ---


# Perform hierarchical clustering on the PCA results to group similar locations
hcpc <- HCPC(pcaSeason, proba = 0.1, method = "ward", metric = "euclidean", nb.clust = 4)
hcpc$desc.var  #most important variables for each cluster 

# Visualize dendogram


# ---
# PCA and Cluster Visualization: seasonal data----
# ---
# Define a color palette for the clusters
col_clust=c("gold"
            ,"red"
            ,"darkgreen"
            ,"blue",
            "black")

# Create a data frame of variable coordinates for plotting
# The coordinates are scaled for better visual representation
coord <- data.frame(PC1 =  pcaSeason$var$coord[,1]*10, 
                    PC2 =  pcaSeason$var$coord[,2]*8)
coord$angle=pracma::rad2deg(atan((0-coord$PC2)/(0-coord$PC1)))

# Create data frames for the coordinates of qualitative supplementary variables
# This allows for plotting them on the PCA biplot
# Coordinates for Locations
coord_quali_locations <- data.frame(
  PC1 = pcaSeason$quali.sup$coord[c(48:112), 1] * 1,
  PC2 = pcaSeason$quali.sup$coord[c(48:112), 2] * 1
)

# Coordinates for Provinces
coord_quali_provinces <- data.frame(
  PC1 = pcaSeason$quali.sup$coord[c(14:47), 1] * 1,
  PC2 = pcaSeason$quali.sup$coord[c(14:47), 2] * 1
)

# Coordinates for Regions
coord_quali_regions <- data.frame(
  PC1 = pcaSeason$quali.sup$coord[c(1:13), 1] * 1,
  PC2 = pcaSeason$quali.sup$coord[c(1:13), 2] * 1
)

#rm(p)
# Initialize the PCA biplot with cluster information
p<-fviz_cluster(hcpc
                ,geom = "point"
                ,proba=0.1
                ,labelsize = 0
                ,pointsize=2
                ,alpha = 1
                ,ellipse.alpha =.5
                , ellipse.type = "convex"
                ,show.clust.cent=T
                , ellipse.border.remove  = FALSE
) 
p
# Apply custom styling to the plot
p<-p+ scale_color_manual(values = col_clust)+
  scale_fill_manual(values = col_clust)+
  
  xlab("Principal Component 1 (62.9%)")+
  ylab("Principal Component 2 (18.7%)")+
  
  theme_classic()+
  theme(legend.position = "top")+
  theme(legend.position = "top",
        text = element_text(size=20),
        legend.text = element_text(size=17),
        legend.key.size = unit(1.5,"line"))+ 
  ggtitle("")
p

# Add variable vectors to the PCA plot
p<-fviz_add(p,coord
            ,color ="Black"
            ,geom="arrow"
            ,linetype="dashed"
            ,labelsize = 4
            ,repel = TRUE
            ,alpha=.3
            ,size=0.2
)
p
# Save the initial PCA plot
ggsave("plot/pcaClusters_first_season.png",width = 15.5, height = 6)

# Add Region labels to the plot
p<-fviz_add(p,coord_quali_regions
            ,color = "black"
            ,geom="text"
            ,labelsize=5
            ,repel = T
            ,alpha=0.3
)
p
ggsave("plot/pcaClusters_region_season.png",width = 13.5, height = 6)

#Add Province labels to the plot
p<-fviz_add(p,coord_quali_provinces,
            ,color = "black"
            ,geom="text"
            ,repel=T
            ,labelsize=5
            ,max.overlaps = 50
)
p
ggsave("plot/pcaClusters_Provinces_season.png",width = 13.5, height = 6)

# Add Location labels to the plot
p<-fviz_add(p,coord_quali_locations,
            ,color = "black"
            ,geom="text"
            ,labelsize=5
            ,repel = TRUE
            ,fontface = "bold"
)
p
ggsave("plot/pcaClusters_Location_season.png",width = 13.5, height = 6)
#remove p
rm(p)


# Generate a dendrogram to visualize hierarchical clustering results.
# Extract dendrogram. SONO LE DIVERSE TECNICHE CHE IDENTIFICANO I CLUSTER DIVERSAMENTE?
tree <- hcpc$call$t$tree  
plot(tree)

# Mapping table: row number → LocationID
mapping_dend <- data.frame(
  number = as.character(1:nrow(PcaDfSeason)), 
  LocationID = PcaDfSeason$LocationID
)

# Replace labels keeping the same original order
tree$labels <- mapping_dend$LocationID[match(tree$labels, mapping_dend$number)]
# Visualize dendrogram
dend <- fviz_dend(tree,
                  k = 4,                                
                  cex = 0.7,                            
                  k_colors = c("#E41A1C", "#377EB8", "#4DAF4A", "#FF7F00"),
                  type = "rectangle",                   
                  rect = TRUE,                          
                  rect_border = "grey30",              
                  rect_fill = TRUE,                     
                  main = "Hierarchical Dendrogram with Clusters", 
                  sub = "",                            
                  xlab = "") +                         
  theme_minimal() +                             
  theme(
    plot.title = element_text(size = 14, face = "bold", hjust = 0.5), 
    axis.text.y = element_text(size = 10, colour = "grey20"),         
    axis.text.x = element_blank(),                                    
    axis.ticks.x = element_blank()                                    
  )
dend
ggsave("plot/pcaClusters_dendongram_season.jpg",dend,width = 14, height = 9)



# Boxplots of Climatic Variables by Cluster
# Prepare the data for boxplot visualization
#df_point=p$data
#df_point$Clusters = df_point$clust
df_hcpc=as.data.frame(hcpc$data.clust) 
df_boxplot <- Seasonal |> 
  left_join(hcpc$data.clust)|> 
 dplyr::select(-c(12:23)) |> 
  drop_na()

names(df_boxplot)

# Define the correct order of the months
season_order <- c("wi","sp","su","fa")

# Factorize 'Month' and relabel 'clust' for better readability
df_boxplot$season <- factor(df_boxplot$season, levels = season_order)

# Factorize 'Month' and relabel 'clust' for better readability
df_boxplot$clust = plyr::revalue(df_boxplot$clust,
                                 c('1'='Cluster 1',
                                   '2'='Cluster 2',
                                   '3'= 'Cluster 3',
                                   '4'= 'Cluster 4'))



# # Generate violin plots for relative humidity (RH) by season and cluster
# p1<-ggplot(df_boxplot,aes(season,rh_mu)) + 
#   geom_violin(alpha = 0.80, size = 0.3) + 
#   #geom_jitter(alpha = 0.3, position = position_jitter(width = 0.1), 
#   #            size = 1, shape=4) +
#   stat_summary(fun.y = mean, colour = "darkblue", geom = "line", group = 1, lwd = 0.3, lty = 2) +
#   stat_summary(fun.y = mean, colour = "darkblue", geom = "point",size=2)+
#   scale_fill_manual(values=c("cyan","orange","red","brown", "green"))+
#   theme(legend.position = "top")+
#   facet_wrap(~clust,ncol=4) +
#   theme_classic()+
#   xlab("")+ylab("RH minimum (%)")
# p1
# 
# # Customize facet strip colors
# g <- ggplot_gtable(ggplot_build(p1))
# stripr <- which(grepl('strip-t', g$layout$name))
# fills <- col_clust
# k <- 1
# for (i in stripr) {
#   j <- which(grepl('rect', g$grobs[[i]]$grobs[[1]]$childrenOrder))
#   g$grobs[[i]]$grobs[[1]]$children[[j]]$gp$fill <- fills[k]
#   k <- k+1
# }
# grid::grid.newpage()
# grid::grid.draw(g)
# g<-gridExtra::arrangeGrob(g)
# #ggsave(paste0(thisDir,"\\plots//pcaBoxRhn.png"),g,width = 5, height = 2.5)

# Generate violin plots for minimum temperature (Tn)
p1<-ggplot(df_boxplot,aes(season,tn_mu)) + 
  geom_violin(alpha = 0.80, size = 0.3) + 
  #geom_jitter(alpha = 0.3, position = position_jitter(width = 0.1), 
  #            size = 1, shape=4) +
  stat_summary(fun.y = mean, colour = "darkblue", geom = "line", group = 1, lwd = 0.3, lty = 2) +
  stat_summary(fun.y = mean, colour = "darkblue", geom = "point",size=2)+
  scale_fill_manual(values=c("cyan","orange","red","brown"))+
  theme(legend.position = "top")+
  facet_wrap(~clust,ncol=4) +
  theme_classic()+
  xlab("")+ylab("T minimum (°C)")
p1
# Customize facet strip colors
g <- ggplot_gtable(ggplot_build(p1))
stripr <- which(grepl('strip-t', g$layout$name))
fills <- col_clust
k <- 1
for (i in stripr) {
  j <- which(grepl('rect', g$grobs[[i]]$grobs[[1]]$childrenOrder))
  g$grobs[[i]]$grobs[[1]]$children[[j]]$gp$fill <- fills[k]
  k <- k+1
}
grid::grid.newpage()
grid::grid.draw(g)
g<-gridExtra::arrangeGrob(g)
ggsave("plot/pcaBoxTn.png",g,width = 5, height = 2.5)


# Generate violin plots for maximum temperature (Tx)
p1<-ggplot(df_boxplot,aes(season,tx_mu)) + 
  geom_violin(alpha = 0.80, size = 0.3) + 
  #geom_jitter(alpha = 0.3, position = position_jitter(width = 0.1), 
  #            size = 1, shape=4) +
  stat_summary(fun.y = mean, colour = "darkblue", geom = "line", group = 1, lwd = 0.3, lty = 2) +
  stat_summary(fun.y = mean, colour = "darkblue", geom = "point",size=2)+
  scale_fill_manual(values=c("cyan","orange","red","brown"))+
  theme(legend.position = "top")+
  facet_wrap(~clust,ncol=4) +
  theme_classic()+
  xlab("")+ylab("T maximum (°C)")
p1
# Customize facet strip colors
g <- ggplot_gtable(ggplot_build(p1))
stripr <- which(grepl('strip-t', g$layout$name))
fills <- col_clust
k <- 1
for (i in stripr) {
  j <- which(grepl('rect', g$grobs[[i]]$grobs[[1]]$childrenOrder))
  g$grobs[[i]]$grobs[[1]]$children[[j]]$gp$fill <- fills[k]
  k <- k+1
}
grid::grid.newpage()
grid::grid.draw(g)
g<-gridExtra::arrangeGrob(g)
ggsave("plot/pcaBoxTx.png",g,width = 5, height = 2.5)


# Generate violin plots for precipitation (prc)
p1<-ggplot(df_boxplot,aes(season,prc_mu)) + 
  geom_violin(alpha = 0.80, size = 0.3) + 
  #geom_jitter(alpha = 0.3, position = position_jitter(width = 0.1), 
  #            size = 1, shape=4) +
  stat_summary(fun.y = mean, colour = "darkblue", geom = "line", group = 1, lwd = 0.3, lty = 2) +
  stat_summary(fun.y = mean, colour = "darkblue", geom = "point",size=2)+
  scale_fill_manual(values=c("cyan","orange","red","brown"))+
  theme(legend.position = "top")+
  facet_wrap(~clust,ncol=4) +
  theme_classic()+
  xlab("")+ylab("Precipitation (mm)")
p1
# Customize facet strip colors
g <- ggplot_gtable(ggplot_build(p1))
stripr <- which(grepl('strip-t', g$layout$name))
fills <- col_clust
k <- 1
for (i in stripr) {
  j <- which(grepl('rect', g$grobs[[i]]$grobs[[1]]$childrenOrder))
  g$grobs[[i]]$grobs[[1]]$children[[j]]$gp$fill <- fills[k]
  k <- k+1
}
grid::grid.newpage()
grid::grid.draw(g)
g<-gridExtra::arrangeGrob(g)
ggsave("plot/plotpcaBoxRain.png",g,width = 5, height = 2.5)


# Extracting and Saving Representative Individuals

colnames(hcpc$data.clust)
#extract first representative individuals
s<-hcpc$desc.ind$para
t<-unlist(hcpc$desc.ind$para[[1]])

# Extract and combine the most representative individuals and the most extreme individuals for each cluster
# This helps in identifying locations that best define each cluster's characteristics
cluster1RepInd<- hcpc$data.clust[names(hcpc$desc.ind$para[[1]][1]),] |> 
  dplyr::select(1,2,3,20)
cluster1ExtInd<- hcpc$data.clust[names(hcpc$desc.ind$dist[[1]][1]),] |> 
  dplyr::select(1,2,3,20)
cluster2RepInd<- hcpc$data.clust[names(hcpc$desc.ind$para[[2]][1]),]|> 
  dplyr::select(1,2,3,20)
cluster2ExtInd<- hcpc$data.clust[names(hcpc$desc.ind$dist[[2]][1]),] |> 
  dplyr::select(1,2,3,20)
cluster3RepInd<- hcpc$data.clust[names(hcpc$desc.ind$para[[3]])[1],]|> 
  dplyr::select(1,2,3,20)
cluster3ExtInd<- hcpc$data.clust[names(hcpc$desc.ind$dist[[3]])[1],]|> 
  dplyr::select(1,2,3,20)
cluster4RepInd<- hcpc$data.clust[names(hcpc$desc.ind$para[[4]])[1],]|> 
  dplyr::select(1,2,3,20)
cluster4ExtInd<- hcpc$data.clust[names(hcpc$desc.ind$dist[[4]])[1],]|> 
  dplyr::select(1,2,3,20)
# Combine all representative individuals into a single data frame
sensitivity<-rbind(cluster1RepInd,cluster1ExtInd,cluster2RepInd,cluster2ExtInd,
                   cluster3RepInd,cluster3ExtInd,cluster4RepInd,cluster4ExtInd)
# Save the combined data frame to a CSV file for future use or analysis
write.csv(sensitivity, paste0("Sensitivity_season.csv"), 
          row.names = F)


# Table with weather variable statistics
# for each cluster
clus_stat <- PcaDfSeason |>
  group_by(cluster) |>
  summarise(across(
    .cols = c(
      tx_mu_wi:prc_mu_fa 
    ),
    .fns = mean
  ))



# Mapping clusters by location 
# Prepare the data frame for mapping by selecting necessary columns
df_map<- PcaDfSeason |>
  select(Region,Province,LocationID,lon,lat,cluster)

#libraries
library(sf)
library(rnaturalearth)
library(rnaturalearthdata)
library(RColorBrewer)
library(ggrepel)

# Download Italian administrative boundaries
Italy <- ne_countries(scale = "medium", country = "Italy", returnclass = "sf")
# Add a 'PointID' column
df_map <- df_map |>
  mutate(PointID = 1:n())  # Assign seq number to each row
#plot
p_map <- ggplot() +
  geom_sf(data = Italy, fill = "gray95", color = "black") +
  geom_point(data = df_map,
             aes(x = lon, y = lat, color = cluster),
             size = 2.5, alpha = 0.9) +
  geom_text_repel(data = df_map,
                  aes(x = lon, y = lat, label = PointID, color = cluster),  # Aggiunto color = cluster qui
                  size = 2.5,
                  max.overlaps = 20,
                  fontface = "bold",
                  direction = "y",      # move vertically
                  hjust = 0,            # Align on the left (from the point)
                  nudge_x = 0.1,        # move to right (from the point)
                  box.padding = 0.2,
                  point.padding = 0.1,
                  min.segment.length = 0,
                  segment.size = 0.3,
                  segment.color = "gray50",
                  segment.alpha = 0.5,
                  show.legend = FALSE) +  # No legend (created in a separate obj)
  scale_color_brewer(palette = "Set1") +
  coord_sf(xlim = c(6, 19), ylim = c(36, 48), expand = FALSE) +
  theme_minimal(base_size = 12) +
  labs(title = "Clusters distribution",
       x = "Longitude", y = "Latitude", color = "Cluster")

p_map
# Save map
ggsave("plot/ClusterItalyMap_SEASON.jpg", p_map, width = 8, height = 6)
# Legend (separated from plot)
legend_data <- df_map |>
  select(PointID, LocationID, cluster) |>
  arrange(PointID)  # Ordina per numero
#copy legend table
write.table(legend_data, "clipboard", sep="\t", row.names=FALSE, col.names=TRUE)
# Save legend as different obj
writeLines(legend_text, "map_legend.txt")











###########################################################################
###########################################################################
###########################################################################
###########################################################################
## PCA and cluster analysis: monthly data----
### PCA: monthly data----
# Reshape the seasonal data from long to wide format for PCA
# The 'values_from' parameter creates new columns for each climatic metric per season
#add month name column
monthNames <- c("jan","feb","mar","apr", "may", "jun", "jul","aug", "sep","oct","nov","dec")
Monthly$month <- as.character(Monthly$month)
Monthly$month<-plyr::revalue(Monthly$month,c( "1" = monthNames[1],
                                              "2" = monthNames[2],
                                              "3" = monthNames[3],
                                              "4" = monthNames[4],
                                              "5" = monthNames[5],
                                              "6" = monthNames[6],
                                              "7" = monthNames[7],
                                              "8" = monthNames[8],
                                              "9" = monthNames[9],
                                              "10" = monthNames[10],
                                              "11" = monthNames[11],
                                              "12" = monthNames[12]
                                              ))

 

PcaDfMonth <- Monthly |>
  pivot_wider(names_from = month,
              values_from = c(tx_mu, tx_cv, tn_mu, tn_cv, rh_mu, rh_cv, prc_mu, prc_cv)) |>
  drop_na()

# Select variables for PCA, excluding non-numeric and metadata columns
# The qualitative variables (Region, Province, LocationID) are kept for supplementary analysis
pca_data <- PcaDfMonth |> select(-c(1,4,6,7)) # Remove any rows with missing values
# Define the indices of the qualitative supplementary variables
quali_idx <- c(1,2,3) 
# Perform PCA on the standardized data
# The qualitative variables are projected onto the principal components but do not contribute to their calculation 
pcaMonth <- PCA(pca_data, quali.sup = quali_idx, scale.unit = TRUE, # Standardize variables to unit variance
                 graph = FALSE) # Suppress default plots
plot(pcaMonth)

# Visualize the eigenvalues (scree plot) to determine the number of principal components to retain
fviz_screeplot(pcaMonth, addlabels = TRUE)
# Get and print the loadings (coordinates) of the variables on the first two principal components
# These values represent the correlation between each variable and the components
loadings <- pcaMonth$var$coord
print(round(loadings, 3))
# Display a summary of the PCA results, including explained variance per dimension
summary(pcaMonth)

# Loading visualization
# Load libraries for plotting
library(ggplot2)
library(reshape2)
library(pracma)

# Divide the 'Variable' column into two new columns: "VarType" and "Month".
# 'VarType' extracts the type of variable (e.g., "Tx_mu", "Tn_mu").
#load_df <- as.data.frame(loadings[, 1:2]) # Select loadings for PC1 and PC2
#load_df_long$VarType <- sub("_[a-z]+$", "", load_df_long$Variable) # extracts Tx_mu, Tn_mu...
#load_df_long$Month   <- sub(".*_", "", load_df_long$Variable)      # extracts jan, feb...

# Define the correct chronological order for months.
#month_levels <- c("jan","feb","mar","apr","may","jun","jul","aug","sep","oct","nov","dec")
#load_df_long$Month <- factor(load_df_long$Month, levels = month_levels)

# Create a bar plot with facets for each variable type
# p_loadings <- ggplot(load_df_long, aes(x = Month, y = value, fill = variable)) +
#   geom_bar(stat = "identity", position = "dodge") +
#   # Create a separate panel (facet) for each 'VarType', allowing independent x-axis scales
#   facet_wrap(~ VarType, scales = "free_x", ncol = 2) +  
#   labs(title = "PCA Loadings for PC1 and PC2",
#        y = "Loading", x = "Month") +
#   theme_minimal() +
#   theme(
#     axis.text.x = element_text(angle = 45, hjust = 1, size = 10),
#     axis.text.y = element_text(size = 12),
#     plot.title = element_text(size = 14, face = "bold")
#   )+
#   theme(legend.position = "top")
# 
# p_loadings
# ggsave("pca_loadings.jpg", p_loadings, width = 5, height = 6) 
# # Visualize the optimal number of clusters using
# pca_coords <- pcaMonth$ind$coord
# # This method identifies the 'k' where adding another cluster does not significantly reduce the WSS,
# # suggesting a point of diminishing returns for increasing cluster count.
# fviz_nbclust(pca_coords, kmeans, method = "wss")     #Elbow method
# # This method evaluates the quality of clustering by measuring how similar an object is to its own cluster
# # compared to other clusters. Higher silhouette values indicate better-defined clusters.
# fviz_nbclust(pca_coords, kmeans, method = "silhouette") #Silhouette method
# # This method compares the total within-cluster variation for different 'k' with that expected
# # under a null reference distribution (a dataset with no inherent clustering).
# # The optimal 'k' is typically where the Gap statistic is maximized, indicating the strongest clustering structure.
# fviz_nbclust(pca_coords, kmeans, method = "gap_stat") #Gap Statistic method
### HCPC: monthy data----
# (Hierarchical Clustering on Principal Components)

# Perform hierarchical clustering on the PCA results to group similar locations
# The method partitions the data into 4 clusters based on a Euclidean distance metric
#
# metric = "euclidean" -> Straight-line distance (L2) between locations in the PCA space, treating differences across all axes equally.
# method = "single"    -> Agglomeration method ("Single Linkage"): the distance between two clusters is defined by the minimum distance between their closest points.
hcpc <- HCPC(pcaMonth, proba = 0.1, method = "single", metric = "euclidean", nb.clust = 4)
hcpc$desc.var  #most important variables for each cluster 

# Add the cluster assignment to the main data frame for subsequent visualization
PcaDfMonth$cluster <- hcpc$data.clust$clust

## Visualization of Clusters
# Visualize the clusters in the PCA space
fviz_cluster(hcpc,
             geom = "point",
             ellipse.type = "convex",
             palette = "jco",
             repel = TRUE) +
  labs(title = "Clustering Locations on PCA") +
  theme_minimal()

# Visualize PCA clusters with 'LocationID' labels for detailed identification
fviz_pca_ind(pcaMonth,
             habillage = PcaDfMonth$cluster,
             geom = "point",
             palette = "jco",
             repel = TRUE,
             addEllipses = TRUE) +
  geom_text(aes(label = PcaDfMonth$LocationID), vjust = 1.5, size = 2) +
  labs(title = "Clusters with locations")

# Visualize PCA clusters with 'Province' labels
fviz_pca_ind(pcaMonth,
             habillage = PcaDfMonth$cluster,
             geom = "point",
             palette = "jco",
             repel = TRUE,
             addEllipses = TRUE) +
  geom_text(aes(label = PcaDfMonth$Province), vjust = 1.5, size = 2) +
  labs(title = "Clusters with Provinces")

# Visualize PCA clusters with 'Region' labels
fviz_pca_ind(pcaMonth,
             habillage = PcaDfMonth$cluster,
             geom = "point",
             palette = "jco",
             repel = TRUE,
             addEllipses = TRUE) +
  geom_text(aes(label = PcaDfMonth$Region), vjust = 1.5, size = 2) +
  labs(title = "Clusters with Regions ")


# ---
### PCA and Cluster Visualization: mothly data----
# ---

# Define a color palette for the clusters
col_clust=c("gold"
            ,"red"
            ,"darkgreen"
            ,"blue",
            "black")

# Create a data frame of variable coordinates for plotting
# The coordinates are scaled for better visual representation
coord <- data.frame(PC1 =  pcaMonth$var$coord[,1]*10, 
                    PC2 =  pcaMonth$var$coord[,2]*8)
coord$angle=pracma::rad2deg(atan((0-coord$PC2)/(0-coord$PC1)))

# Create data frames for the coordinates of qualitative supplementary variables
# This allows for plotting them on the PCA biplot
# Coordinates for Locations
coord_quali_locations <- data.frame(
  PC1 = pcaMonth$quali.sup$coord[c(48:112), 1] * 1,
  PC2 = pcaMonth$quali.sup$coord[c(48:112), 2] * 1
)

# Coordinates for Provinces
coord_quali_provinces <- data.frame(
  PC1 = pcaMonth$quali.sup$coord[c(14:47), 1] * 1,
  PC2 = pcaMonth$quali.sup$coord[c(14:47), 2] * 1
)

# Coordinates for Regions
coord_quali_regions <- data.frame(
  PC1 = pcaMonth$quali.sup$coord[c(1:13), 1] * 1,
  PC2 = pcaMonth$quali.sup$coord[c(1:13), 2] * 1
)

rm(p)
# Initialize the PCA biplot with cluster information
p<-fviz_cluster(hcpc
                ,geom = "point"
                ,proba=0.1
                ,labelsize = 0
                ,pointsize=2
                ,alpha = 1
                ,ellipse.alpha =.5
                , ellipse.type = "convex"
                ,show.clust.cent=T
                , ellipse.border.remove  = FALSE
) 
p
# Apply custom styling to the plot
p<-p+ scale_color_manual(values = col_clust)+
  scale_fill_manual(values = col_clust)+
  
  xlab("Principal Component 1 (46.9%)")+
  ylab("Principal Component 2 (21.5%)")+
  
  theme_classic()+
  theme(legend.position = "top")+
  theme(legend.position = "top",
        text = element_text(size=20),
        legend.text = element_text(size=17),
        legend.key.size = unit(1.5,"line"))+ 
  ggtitle("")
p

# Add variable vectors to the PCA plot
p<-fviz_add(p,coord
            ,color ="Black"
            ,geom="arrow"
            ,linetype="dashed"
            ,labelsize = 4
            ,repel = TRUE
            ,alpha=.3
            ,size=0.2
)
p
# Save the initial PCA plot
ggsave(paste0(thisDir,"pcaClusters_first.png"),width = 15.5, height = 6)

# Add Region labels to the plot
p<-fviz_add(p,coord_quali_regions
            ,color = "black"
            ,geom="text"
            ,labelsize=5
            ,repel = T
            ,alpha=0.3
)
p
ggsave(paste0(thisDir,"pcaClusters_region.png"),width = 13.5, height = 6)

#Add Province labels to the plot
p<-fviz_add(p,coord_quali_provinces,
            ,color = "Black"
            ,geom="text"
            ,repel=T
            ,labelsize=5
            ,max.overlaps = 50
)
p
ggsave(paste0(thisDir,"pcaClusters_Provinces.png"),width = 13.5, height = 6)

# Add Location labels to the plot
p<-fviz_add(p,coord_quali_locations,
            ,color = "black"
            ,geom="text"
            ,labelsize=5
            ,repel = TRUE
            ,fontface = "bold"
)
p
ggsave(paste0(thisDir,"pcaClusters_Location.png"),width = 13.5, height = 6)
#remove p
#rm(p)


# Generate a dendrogram to visualize hierarchical clustering results.
# Extract dendrogram. SONO LE DIVERSE TECNICHE CHE IDENTIFICANO I CLUSTER DIVERSAMENTE?
tree <- hcpc$call$t$tree  

# Mapping table: row number → LocationID
mapping_dend <- data.frame(
  number = as.character(1:nrow(PcaDfMonth)), 
  LocationID = PcaDfMonth$LocationID
)

# Replace labels keeping the same original order
tree$labels <- mapping_dend$LocationID[match(tree$labels, mapping_dend$number)]
# Visualize dendrogram
dend <- fviz_dend(tree,
          k = 4,                                # Cut the dendrogram into 4 clusters.
          cex = 0.7,                            # Adjust label size for better readability.
          k_colors = c("#E41A1C", "#377EB8", "#4DAF4A", "#FF7F00"), # Apply a more balanced color palette for clusters.
          type = "rectangle",                   # Display the dendrogram in a rectangular shape.
          rect = TRUE,                          # Draw rectangles around clusters.
          rect_border = "grey30",               # Set a neutral border color for cluster rectangles.
          rect_fill = TRUE,                     # Fill the cluster rectangles with color.
          main = "Hierarchical Dendrogram with Clusters", # Main title of the plot (translated from 'Dendrogramma gerarchico con cluster').
          sub = "",                             # Remove the subtitle.
          xlab = "") +                          # Remove the x-axis label.
  theme_minimal() +                             # Apply a minimal theme to the plot.
  theme(
    plot.title = element_text(size = 14, face = "bold", hjust = 0.5), # Customize title appearance (size, bold, centered).
    axis.text.y = element_text(size = 10, colour = "grey20"),         # Customize y-axis text appearance.
    axis.text.x = element_blank(),                                    # Remove x-axis tick labels.
    axis.ticks.x = element_blank()                                    # Remove x-axis ticks.
  )
dend
ggsave("pcaClusters_dendongram.jpg",dend,width = 14, height = 9)


#check differencese betweein k means and tree
table(
  Dendrogram = cutree(tree, k = 4), 
  HCPC_final = hcpc$data.clust$clust
)

# Boxplots of Climatic Variables by Cluster
# Prepare the data for boxplot visualization
df_point=p$data
df_point$Clusters=df_point$clust
df_hcpc=as.data.frame(hcpc$data.clust) 
df_boxplot <- Monthly |> 
  left_join(hcpc$data.clust)|> 
  dplyr::select(-c(17:80)) |> 
  drop_na()

names(df_boxplot)

# Define the correct order of the months
month_order <- c("jan", "feb", "mar", "apr", "may", "jun", 
                 "jul", "aug", "sep", "oct", "nov", "dec")

# Factorize 'Month' and relabel 'clust' for better readability
df_boxplot$month <- factor(df_boxplot$month, levels = month_order)
df_boxplot$clust = plyr::revalue(df_boxplot$clust,
                                 c('1'='Cluster 1',
                                   '2'='Cluster 2',
                                   '3'= 'Cluster 3',
                                   '4'= 'Cluster 4'))



# Generate violin plots for relative humidity (RH) by Month and cluster
p1<-ggplot(df_boxplot,aes(month,rh_mu)) + 
  geom_violin(alpha = 0.80, size = 0.3) + 
  #geom_jitter(alpha = 0.3, position = position_jitter(width = 0.1), 
  #            size = 1, shape=4) +
  stat_summary(fun.y = mean, colour = "darkblue", geom = "line", group = 1, lwd = 0.3, lty = 2) +
  stat_summary(fun.y = mean, colour = "darkblue", geom = "point",size=2)+
  scale_fill_manual(values=c("cyan","orange","red","brown"))+
  theme(legend.position = "top")+
  facet_wrap(~clust,ncol=4) +
  theme_classic()+
  xlab("")+ylab("RH minimum (%)")
p1

# Customize facet strip colors
g <- ggplot_gtable(ggplot_build(p1))
stripr <- which(grepl('strip-t', g$layout$name))
fills <- col_clust
k <- 1
for (i in stripr) {
  j <- which(grepl('rect', g$grobs[[i]]$grobs[[1]]$childrenOrder))
  g$grobs[[i]]$grobs[[1]]$children[[j]]$gp$fill <- fills[k]
  k <- k+1
}
grid::grid.newpage()
grid::grid.draw(g)
g<-gridExtra::arrangeGrob(g)
ggsave("pcaBoxRhn.png",g,width = 12, height = 5)

# Generate violin plots for minimum temperature (Tn)
p1<-ggplot(df_boxplot,aes(month,tn_mu)) + 
  geom_violin(alpha = 0.80, size = 0.3) + 
  #geom_jitter(alpha = 0.3, position = position_jitter(width = 0.1), 
  #            size = 1, shape=4) +
  stat_summary(fun.y = mean, colour = "darkblue", geom = "line", group = 1, lwd = 0.3, lty = 2) +
  stat_summary(fun.y = mean, colour = "darkblue", geom = "point",size=2)+
  scale_fill_manual(values=c("cyan","orange","red","brown"))+
  theme(legend.position = "top")+
  facet_wrap(~clust,ncol=4) +
  theme_classic()+
  xlab("")+ylab("T minimum (°C)")
p1
# Customize facet strip colors
g <- ggplot_gtable(ggplot_build(p1))
stripr <- which(grepl('strip-t', g$layout$name))
fills <- col_clust
k <- 1
for (i in stripr) {
  j <- which(grepl('rect', g$grobs[[i]]$grobs[[1]]$childrenOrder))
  g$grobs[[i]]$grobs[[1]]$children[[j]]$gp$fill <- fills[k]
  k <- k+1
}
grid::grid.newpage()
grid::grid.draw(g)
g<-gridExtra::arrangeGrob(g)
ggsave("pcaBoxTn.png",g,width = 12, height = 5)

# Generate violin plots for maximum temperature (Tx)
p1<-ggplot(df_boxplot,aes(month,tx_mu)) + 
  geom_violin(alpha = 0.80, size = 0.3) + 
  #geom_jitter(alpha = 0.3, position = position_jitter(width = 0.1), 
  #            size = 1, shape=4) +
  stat_summary(fun.y = mean, colour = "darkblue", geom = "line", group = 1, lwd = 0.3, lty = 2) +
  stat_summary(fun.y = mean, colour = "darkblue", geom = "point",size=2)+
  scale_fill_manual(values=c("cyan","orange","red","brown"))+
  theme(legend.position = "top")+
  facet_wrap(~clust,ncol=4) +
  theme_classic()+
  xlab("")+ylab("T maximum (°C)")
p1
# Customize facet strip colors
g <- ggplot_gtable(ggplot_build(p1))
stripr <- which(grepl('strip-t', g$layout$name))
fills <- col_clust
k <- 1
for (i in stripr) {
  j <- which(grepl('rect', g$grobs[[i]]$grobs[[1]]$childrenOrder))
  g$grobs[[i]]$grobs[[1]]$children[[j]]$gp$fill <- fills[k]
  k <- k+1
}
grid::grid.newpage()
grid::grid.draw(g)
g<-gridExtra::arrangeGrob(g)
ggsave("pcaBoxTx.png",g,width = 12, height = 5)

# Generate violin plots for precipitation (prc)
p1<-ggplot(df_boxplot,aes(month,prc_mu)) + 
  geom_violin(alpha = 0.80, size = 0.3) + 
  #geom_jitter(alpha = 0.3, position = position_jitter(width = 0.1), 
  #            size = 1, shape=4) +
  stat_summary(fun.y = mean, colour = "darkblue", geom = "line", group = 1, lwd = 0.3, lty = 2) +
  stat_summary(fun.y = mean, colour = "darkblue", geom = "point",size=2)+
  scale_fill_manual(values=c("cyan","orange","red","brown"))+
  theme(legend.position = "top")+
  facet_wrap(~clust,ncol=4) +
  theme_classic()+
  xlab("")+ylab("Precipitation (mm)")
p1
# Customize facet strip colors
g <- ggplot_gtable(ggplot_build(p1))
stripr <- which(grepl('strip-t', g$layout$name))
fills <- col_clust
k <- 1
for (i in stripr) {
  j <- which(grepl('rect', g$grobs[[i]]$grobs[[1]]$childrenOrder))
  g$grobs[[i]]$grobs[[1]]$children[[j]]$gp$fill <- fills[k]
  k <- k+1
}
grid::grid.newpage()
grid::grid.draw(g)
g<-gridExtra::arrangeGrob(g)
ggsave("pcaBoxRain.png",g,width = 12, height = 5)


# Extracting and Saving Representative Individuals

colnames(hcpc$data.clust)
# Extract first representative individuals
s<-hcpc$desc.ind$para
t<-unlist(hcpc$desc.ind$para[[1]])

# Extract and combine the most representative individuals and the most extreme individuals for each cluster
# This helps in identifying locations that best define each cluster's characteristics
cluster1RepInd<- hcpc$data.clust[names(hcpc$desc.ind$para[[1]][1]),] |> 
  dplyr::select(1,2,3,100)
cluster1ExtInd<- hcpc$data.clust[names(hcpc$desc.ind$dist[[1]][1]),] |> 
  dplyr::select(1,2,3,100)
cluster2RepInd<- hcpc$data.clust[names(hcpc$desc.ind$para[[2]][1]),]|> 
  dplyr::select(1,2,3,100)
cluster2ExtInd<- hcpc$data.clust[names(hcpc$desc.ind$dist[[2]][1]),] |> 
  dplyr::select(1,2,3,100)
cluster3RepInd<- hcpc$data.clust[names(hcpc$desc.ind$para[[3]])[1],]|> 
  dplyr::select(1,2,3,100)
cluster3ExtInd<- hcpc$data.clust[names(hcpc$desc.ind$dist[[3]])[1],]|> 
  dplyr::select(1,2,3,100)
cluster4RepInd<- hcpc$data.clust[names(hcpc$desc.ind$para[[4]])[1],]|> 
  dplyr::select(1,2,3,100)
cluster4ExtInd<- hcpc$data.clust[names(hcpc$desc.ind$dist[[4]])[1],]|> 
  dplyr::select(1,2,3,100)
# Combine all representative individuals into a single data frame
sensitivity<-rbind(cluster1RepInd,cluster1ExtInd,cluster2RepInd,cluster2ExtInd,
                   cluster3RepInd,cluster3ExtInd,cluster4RepInd,cluster4ExtInd)
# Save the combined data frame to a CSV file for future use or analysis
write.csv(sensitivity, paste0("sensitivity.csv"), 
          row.names = F)

# Table with weather variable statistics
# for each cluster
clus_stat <- PcaDfMonth |>
  group_by(cluster) |>
  summarise(across(
    .cols = c(
      tx_mu_jan:prc_cv_dec  
    ),
    .fns = mean
  ))

#Plot climatic variable (mean and variability)
# Long format
clus_long <- clus_stat |>
  pivot_longer(
    cols = -cluster,
    names_to = "variable",
    values_to = "value"
  ) |>
  separate(variable, into = c("var", "stat", "month"), sep = "_")

month_order <- c("jan", "feb", "mar", "apr", "may", "jun", 
                 "jul", "aug", "sep", "oct", "nov", "dec")

# Factorize 'Month' and relabel 'clust' for better readability
clus_long$month <- factor(clus_long$month, levels = month_order)
# Mean plot
p_mean <- ggplot(
  data = filter(clus_long, stat == "mu"),
  aes(x = month, y = value, color = var, group = var)
) +
  geom_line(size = 1) +
  geom_point() +
  facet_wrap(~ cluster) +
  labs(
    title = "Monthly climatic average trends by cluster",
    x = "Month",
    y = "avg value",
    color = "Variable"
  ) +
  theme_minimal()
ggsave("Monthly climatic average trends by cluster.png", p_mean, width = 8, height = 4)

# Variability plot
library(scales)
p_cv <- ggplot(
  data = filter(clus_long, stat == "cv"),
  aes(x = month, y = value, color = var, group = var)
) +
  geom_line(size = 1, linetype = "dashed") +
  geom_point() +
  facet_wrap(~ cluster) +
  scale_y_continuous(
    limits = c(0, 400),
    oob = squish) +
    labs(
    title = "Monthly climatic Variability by cluster",
    x = "Month",
    y = "Coefficient of variation",
    color = "Variable"
  ) +
  theme_minimal()
p_cv
ggsave("Monthly climatic Variability by cluster.png", p_cv, width = 8, height = 4)




# Mapping clusters by location 
# Prepare the data frame for mapping by selecting necessary columns
df_map<- PcaDfMonth |>
  select(Region,Province,LocationID,lon,lat,cluster)

#libraries
library(ggplot2)
library(sf)
library(rnaturalearth)
library(rnaturalearthdata)
library(RColorBrewer)
library(ggrepel)

# Download Italian administrative boundaries
Italy <- ne_countries(scale = "medium", country = "Italy", returnclass = "sf")
# Add a 'PointID' column
df_map <- df_map |>
  mutate(PointID = 1:n())  # Assign seq number to each row
# Plot
p_map <- ggplot() +
  geom_sf(data = Italy, fill = "gray95", color = "black") +
  geom_point(data = df_map,
             aes(x = lon, y = lat, color = cluster),
             size = 2.5, alpha = 0.9) +
  geom_text_repel(data = df_map,
                  aes(x = lon, y = lat, label = PointID, color = cluster),  # Aggiunto color = cluster qui
                  size = 2.5,
                  max.overlaps = 20,
                  fontface = "bold",
                  direction = "y",      # move vertically
                  hjust = 0,            # Align on the left (from the point)
                  nudge_x = 0.1,        # move to right (from the point)
                  box.padding = 0.2,
                  point.padding = 0.1,
                  min.segment.length = 0,
                  segment.size = 0.3,
                  segment.color = "gray50",
                  segment.alpha = 0.5,
                  show.legend = FALSE) +  # No legend (created in a separate obj)
  scale_color_brewer(palette = "Set1") +
  coord_sf(xlim = c(6, 19), ylim = c(36, 48), expand = FALSE) +
  theme_minimal(base_size = 12) +
  labs(title = "Clusters distribution",
       x = "Longitude", y = "Latitude", color = "Cluster")

p_map
# Save map
ggsave("ClusterItalyMap.jpg", p_map, width = 8, height = 6)
# Legend (separated from plot)
legend_data <- df_map |>
  select(PointID, LocationID, cluster) |>
  arrange(PointID)  # Ordina per numero
# Copy legend table
write.table(legend_data, "clipboard", sep="\t", row.names=FALSE, col.names=TRUE)
# Save legend as different obj
writeLines(legend_text, "map_legend.txt")























