//the input class for the weather data (Input) and the daily weather data to estimate hourly data and to be added to the Output class (InputDaily)
namespace Models.Datatype
{
    //input class for the weather data
    public class ReferenceData
    {
        //reference data
        public Dictionary<string, Dictionary<int, DateTime>> Site_Year_OnsetDate = 
            new Dictionary<string, Dictionary<int, DateTime>>();
    }
}
