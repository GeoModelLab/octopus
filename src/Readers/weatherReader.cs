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
    }
}
