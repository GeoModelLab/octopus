using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Models.Datatype;


namespace Utils
{

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
        public static float temperature_function(double Temperature, double Tmax,
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
            return (float)Tfunction;
        }

        public static GenericInfection incubationEstimate(GenericInfection infection, 
            parametersIncubation parameters, Input input)
        {
            //local variable to return
            GenericInfection thisInfection = infection;

            double tempResponse = temperature_function(input.Temperature, parameters.tmaxIncubation,
                parameters.tminIncubation,parameters.toptIncubation);

            //optimal duration of the incubation period
            float incubationDuration = parameters.incubationDuration * 24;

            //incubation progress update
            infection.incubationProgress = infection.incubationProgress + (float)tempResponse;

            //incubation period ended
            if(infection.incubationProgress >= incubationDuration && 
                infection.onsetDate.Year == 1)
            {
                infection.onsetDate = input.Date;
                infection.incubationProgress = 0;
            }

            return thisInfection;

            //Estimates the incubation period by modulating a baseline value with a temperature function.
            //1.baseline incubation period: xpected incubation duration under standard conditions.
            //2.This baseline value is then adjusted by the temperature modulation function, 
            //providing a value typically ranging from 0.0 to 1.0,
            //based on the cumulative temperature and reflects how environmental conditions can accelerate or delay the incubation

        }
    }
}
