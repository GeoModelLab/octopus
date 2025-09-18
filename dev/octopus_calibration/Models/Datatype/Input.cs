//the input class for the weather data (Input) and the daily weather data to estimate hourly data and to be added to the Output class (InputDaily)
namespace Models.Datatype
{
    //input class for the weather data
    public class Input
    {
        //date of the weather data
        public DateTime Date { get; set; }
        //precipitation in mm
        public float Precipitation { get; set; }
        //air temperature in °C
        public float Temperature { get; set; }
        //leaf wetness (1 = wet, 0 = dry)
		public float LeafWetness { get; set; }
        //air relative humidity in %
        public float RelativeHumidity { get; set; }
    }

    //daily weather data to be added to the Output class
    public class InputDaily 
    {
        //date of the weather data
        public DateTime Date { get; set; }
        //precipitation in mm
        public float Precipitation { get; set; }
        //maximum daily air temperature in °C
        public float Tmax { get; set; }
        //minimum daily air temperature in °C
        public float Tmin { get; set; }
        //number of leaf wetness hours
        public float LeafWetnessDuration { get; set; }
        //maximum daily air relative humidity in %
        public float RHmax { get; set; }
        //minimum daily air relative humidity in %
        public float RHmin { get; set; }
        //name of the weather station
        public string Site { get; set; }
        //dewpoint (for hourly estimates)
        public float dewPoint { get; set; }

    }
}
