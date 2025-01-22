using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;


namespace Utils
{
    public class ColorConfig
    {
        public List<ConsoleColor> SelectedColors { get; private set; } //risk level colors
        public ConsoleColor ConsoleTextColor { get; private set; } //Text color

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

        public ColorConfig(string fileName) //json file
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

                string selectedColorScale = colorConfig?.settings?.colorScale ?? "Viridis"; //Viridis is the default color scale
                SelectedColors = new List<ConsoleColor>();
                ConsoleTextColor = new();

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
                string consoleTextColor = colorConfig?.settings?.consoleTextColor ?? "White"; //White is the default color 
                if (ColorMapping.TryGetValue(consoleTextColor, out var textColor))
                {
                    ConsoleTextColor = textColor;
                }
                else
                {
                    Console.WriteLine($"Warning: '{consoleTextColor}' is not a valid ConsoleColor. Using White as fallback.");
                    ConsoleTextColor = ConsoleColor.White;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading color configuration: {ex.Message}");
                SelectedColors = new List<ConsoleColor> { ConsoleColor.White };
            }
        }
    }

    // JSON Deserialization Classes
    public class Root
    {
        public Settings settings { get; set; }
        public Dictionary<string, List<string>> colorScales { get; set; }
    }

    public class Settings
    {
        public string colorScale { get; set; }
        public string consoleTextColor { get; set; }
    }
}