using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Models.Datatype;


namespace Utils
{
    public class Utils
    {
        //console color configuration
        public List<ConsoleColor> SelectedColors { get; private set; } //risk level colors
        public ConsoleColor ConsoleTextColor { get; private set; } //Text color

        public ConsoleColor ConsoleBackground { get; private set; } //background color

        // Color mapping with System.ConsoleColor 
        private readonly Dictionary<string, ConsoleColor> ColorMapping = new()
    {
        { "Black", ConsoleColor.Black },
        { "DarkBlue", ConsoleColor.DarkBlue },
        { "DarkGreen", ConsoleColor.DarkGreen },
        { "DarkCyan", ConsoleColor.DarkCyan },
        { "DarkRed", ConsoleColor.DarkRed },
        { "DarkMagenta", ConsoleColor.DarkMagenta },
        { "DarkYellow", ConsoleColor.DarkYellow },
        { "Gray", ConsoleColor.Gray },
        { "DarkGray", ConsoleColor.DarkGray },
        { "Blue", ConsoleColor.Blue },
        { "Green", ConsoleColor.Green },
        { "Cyan", ConsoleColor.Cyan },
        { "Red", ConsoleColor.Red },
        { "Magenta", ConsoleColor.Magenta },
        { "Yellow", ConsoleColor.Yellow },
        { "White", ConsoleColor.White }
    };

        public Utils(string fileName) //json file
        {
            LoadColorsFromJson(fileName);
        }

        //deserialization of scale's colors in the json
        private void LoadColorsFromJson(string fileName)
        {
            try
            {
                string jsonString = File.ReadAllText(fileName);
                var colorConfig = JsonSerializer.Deserialize<Root>(jsonString);

                //scale colors for welcome message and risk classification in the console
                string selectedColorScale = colorConfig?.settings?.colorScale ?? "Viridis"; //Viridis is the default color scale
                SelectedColors = new List<ConsoleColor>();
                ConsoleTextColor = new();
                ConsoleBackground = new();
                
                if (colorConfig.colorScales.TryGetValue(selectedColorScale, out var colorNames)) 
                {
                    foreach (var colorName in colorNames)
                    {
                        if (ColorMapping.TryGetValue(colorName, out var consoleColor))
                        {
                            SelectedColors.Add(consoleColor);
                        }
                        else
                        {
                            Console.WriteLine($"Warning: '{colorName}' is not a valid ConsoleColor. Using White as fallback.");
                            SelectedColors.Add(ConsoleColor.White);
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Color scale '{selectedColorScale}' not found. Using default 'Viridis'.");
                    SelectedColors = new List<ConsoleColor>
                    {
                    ConsoleColor.DarkMagenta, ConsoleColor.DarkBlue, ConsoleColor.Cyan, ConsoleColor.Green, ConsoleColor.Yellow
                    };
                }
                //color for console text
                string consoleTextColor = colorConfig?.settings?.consoleTextColor ?? "White"; //White is the default color 
                if (ColorMapping.TryGetValue(consoleTextColor, out var textColor))
                {
                    ConsoleTextColor = textColor;
                }
                else
                {
                    Console.WriteLine($"Warning: '{consoleTextColor}' is not a valid ConsoleColor. Using White as fallback."); //setting Text console color
                    ConsoleTextColor = ConsoleColor.White;
                }
                //color for console background
                string consoleBackground = colorConfig?.settings?.consoleBackgroundColor ?? "Black"; //Black is the default color 
                if (ColorMapping.TryGetValue(consoleBackground, out var backgroundcolor))
                {
                    ConsoleBackground = backgroundcolor;
                }
                else
                {
                    Console.WriteLine($"Warning: '{consoleBackground}' is not a valid ConsoleColor. Using Black as fallback.");
                    ConsoleBackground = ConsoleColor.Black;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading color configuration: {ex.Message}");
                SelectedColors = new List<ConsoleColor> { ConsoleColor.White };
            }
        }
    }

    // JSON Deserialization Classes (color configuration)
    public class Root
    {
        public Settings settings { get; set; }
        public Dictionary<string, List<string>> colorScales { get; set; }
    }

    public class Settings
    {
        public string colorScale { get; set; }
        public string consoleTextColor { get; set; }
        public string consoleBackgroundColor { get; set; }
    }


    //class for other utilities
    public static class utilities
    {   //temperature function
        public static double temperature_function(double Temperature, double Tmax,
       double Tmin, double Topt)
        {
            double Tfunction = 0;

            if (Temperature < Tmin || Temperature > Tmax)
            {
                Tfunction = 0;
            }
            else
            {
                double firstTerm = (Tmax - Temperature) /
                        (Tmax - Topt);
                double secondTerm = (Temperature - Tmin) /
                                     (Topt - Tmin);
                double Exponential = (Topt - Tmin) /
                                     (Tmax - Topt);

                Tfunction = firstTerm * Math.Pow(secondTerm, Exponential);
            }
            return Tfunction;
        }

        public static void incubationEstimate (GenericInfection infection, incubationParameters parameters, float temperature)
        {
            //TODO: finish incubation method
            double tempResponse = temperature_function(temperature, parameters.tmaxIncubation,parameters.tminIncubation,parameters.toptIncubation);

            //Estimates the incubation period by modulating a baseline value with a temperature function.
            //1.baseline incubation period: xpected incubation duration under standard conditions.
            //2.This baseline value is then adjusted by the temperature modulation function, 
            //providing a value typically ranging from 0.0 to 1.0,
            //based on the cumulative temperature and reflects how environmental conditions can accelerate or delay the incubation


        }
    }
}
