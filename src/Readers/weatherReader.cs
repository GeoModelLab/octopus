using Models.Datatype;

namespace octoPusAI.Readers
{
    //This class reads the weather from the file
    internal class WeatherReader
    {
        //The Dictionary has the date as key and the Input data class as value
        public Dictionary<DateTime, Input> readHourly(string file, int startYear, int endYear)
        {
            //Dictionary to store the weather data
            Dictionary<DateTime, Input> gridWeathers = new Dictionary<DateTime, Input>();

            //read the file
            using (var sr = new StreamReader(new BufferedStream(new FileStream(file, FileMode.Open))))
            {
                //skip the first line
                sr.ReadLine();

                //loop over the file
                while (!sr.EndOfStream)
                {
                    //split the line by comma (adjust the split according to the settings of your laptop)
                    string[] line = sr.ReadLine().Split(',');

                    //create a new Input object
                    Input gw = new Input();
               
                    //date elements
                    int year = int.Parse(line[1]);
                    int month = int.Parse(line[2]);
                    int day = int.Parse(line[3]);
                    int hour = int.Parse(line[4]);
                    //set the date
                    gw.Date = new DateTime(year,month,day).AddHours(hour-1);

                    //check if the date is in the range indicated in the configuration file
                    if (gw.Date.Year >= startYear && gw.Date.Year <= endYear)
                    {
                        gw.Temperature = float.Parse(line[5]);
                        gw.Precipitation = float.Parse(line[6]);
                        gw.RelativeHumidity = float.Parse(line[7]);
                        gw.LeafWetness = float.Parse(line[8]);

                        //add the weather to the dictionary
                        if (!gridWeathers.ContainsKey(gw.Date))
                            gridWeathers.Add(gw.Date, gw);

                    }
                }
                //close the stream
                sr.Close();

                //message to console if the date is out of the range
                if (gridWeathers.Count==0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("There are no dates in the weather {2} file from {0} to {1}", startYear, endYear, file);
                }
            }

            //return the dictionary
            return gridWeathers;

        }

        //reader of the daily data, date as key and the InputDaily data class as value
        public Dictionary<DateTime, InputDaily> readDaily(string file, int startYear, int endYear)
        {
            Dictionary<DateTime, InputDaily> gridWeathersDaily = new Dictionary<DateTime, InputDaily>();

            using (var sr = new StreamReader(new BufferedStream(new FileStream(file, FileMode.Open))))
            {
                //skip the first line
                sr.ReadLine();

                //loop over the file
                while (!sr.EndOfStream)
                {
                    //split the line by comma (adjust the split according to the settings of your laptop)
                    string[] line = sr.ReadLine().Split(',');

                    //create a new Input object
                    InputDaily id = new InputDaily();

                    //date elements
                    int year = int.Parse(line[1]);
                    int month = int.Parse(line[2]);
                    int day = int.Parse(line[3]);
                    DateTime date = new DateTime(year, month, day);
                    //set the date
                    id.Date = date;

                    //check if the date is in the range indicated in the configuration file
                    if (id.Date.Year >= startYear && id.Date.Year <= endYear)
                    {
                        id.Tmax = float.Parse(line[4]);
                        id.Tmin = float.Parse(line[5]);
                        id.Precipitation = float.Parse(line[6]);
                        id.dewPoint = dewPoint(id.Tmax, id.Tmin);

                        //add the weather to the dictionary
                        if (!gridWeathersDaily.ContainsKey(id.Date))
                            gridWeathersDaily.Add(id.Date, id);

                    }
                }
                //close the stream
                sr.Close();
            }

            //now I have a dictionary that contains daily Tmax, Tmin, and rain
            return gridWeathersDaily;
        }

        //method to calculate dewpoint t
        private float dewPoint(float tmax, float tmin)
        {
            return 0.38F * tmax - 0.018F *
                (float)Math.Pow(tmax, 2) + 1.4F * tmin - 5F;
        }

        //method to estimate hourly data
        public Dictionary<DateTime, Input> estimateHourly(InputDaily inputDaily, DateTime date)
        {
            //instantiate the hourly class (Input)
            Dictionary<DateTime, Input> gridWeathers = new Dictionary<DateTime, Input>();

            float avgT = (inputDaily.Tmax + inputDaily.Tmin) / 2;   // daily avg t
            float dailyRange = inputDaily.Tmax - inputDaily.Tmin;   // daily t range
            float dewPoint = inputDaily.dewPoint;                   // dew point
            float rain = inputDaily.Precipitation;                  // daily prec

            //loop over the 24 hours in a day and estimate variables
            for(int i = 0; i < 24; i++)
            {
                //instance of the input class
                Input gw = new Input();

                //handle the date
                gw.Date = date.AddHours(i);
                
                //estimates
                //Temp
                float hourlyT = (float)(avgT + dailyRange / 2 * Math.Cos(0.2618f * (i - 15)));
                gw.Temperature = hourlyT;

                //RH
                float es = 0.61121f * (float)Math.Exp((17.502f * hourlyT) / (240.97F + hourlyT));
                float ea = 0.61121f * (float)Math.Exp((17.502F * dewPoint) / (240.97F + dewPoint));
                float rh_hour = ea / es * 100;
                if (rh_hour > 100)
                {
                    rh_hour = 100;
                }
                gw.RelativeHumidity = rh_hour;

                //Rain
                if (rain >= 0.2)
                {
                    gw.Precipitation = rain / 24;
                }
                else
                {
                    gw.Precipitation = 0;

                }

                //leaf wetness ROUGHLY ESTIMATED ---TO CHECK W SIMONE
                if (gw.Precipitation > 0 || gw.RelativeHumidity > 90)
                    gw.LeafWetness = 1;
                else
                    gw.LeafWetness = 0;

                gridWeathers.Add(gw.Date, gw);
            }

            return gridWeathers;
        }
    }
}
