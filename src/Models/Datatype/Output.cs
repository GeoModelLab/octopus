
namespace Models.Datatype
{
    //The base Output class, which is implemented in all the output classes of specific models
    public class Output
    {
        //input weather data
        public Input weatherInputHourly = new Input();
        //daily outputs of the models
        public OutputsDaily outputsDaily = new OutputsDaily();
        //specific model outputs
        public OutputsRule310 outputsRule310 = new OutputsRule310();
        public OutputsLaore outputsLaore = new OutputsLaore();
        public OutputsMagarey outputsMagarey = new OutputsMagarey();
        public OutputsDMCast outputsDMCast = new OutputsDMCast();
        public OutputsEPI outputsEPI = new OutputsEPI();
        public OutputsMisfits outputsMisfits = new OutputsMisfits();
        public OutputsIPI outputsIPI = new OutputsIPI();
        public OutputsUCSC outputsUCSC = new OutputsUCSC();
        //phenology model outputs
        public OutputsPhenology outputsPhenology = new OutputsPhenology();

    }

    //The daily outputs of the models
    public class OutputsDaily
    {
        //the daily weather data as input for the models
        public InputDaily Input = new InputDaily();

        #region integer variables for downy mildew infection (0 = no, 1 = yes)
        public int infectionRule310 { get; set; }
        public int infectionEPI { get; set; }
        public int infectionIPI { get; set; }
        public int infectionDMCast { get; set; }
        public int infectionMagarey { get; set; }
        public int infectionUCSC { get; set; }
        public int infectionMisfits { get; set; }
        public int infectionLaore { get; set; }
        #endregion

        #region integer variables for disease pressure (cumulated number of infections)
        public int pressureRule310 { get; set; }
        public int pressureEPI { get; set; }
        public int pressureIPI { get; set; }
        public int pressureDMCast { get; set; }
        public int pressureMagarey { get; set; }
        public int pressureUCSC { get; set; }
        public int pressureMisfits { get; set; }
        public int pressureLaore { get; set; }
        #endregion

        #region additional model outputs
        public double zoosporesCausingInfection { get; set; }
        public double Tmean { get; set; }
        public double RHmean { get; set; }
        public int Magarey_GER { get; set; }
        public double EPI_ke { get; set; }
        public double EPI_pe { get; set; }
        public double EPI_index { get; set; }
        public int EPI_infections { get; set; }
        public double DMCast_Pom { get; set; }
        public double DMCast_PomSum { get; set; }
        public double DMCast_Ra { get; set; }
        public double DMCast_ooGerm { get; set; }
        public double IPI_Ri { get; set; }
        public double IPI_Tmini { get; set; }
        public double IPI_Tmeani { get; set; }
        public double IPI_Lwi { get; set; }
        public double IPI_Rhi { get; set; }
        public double IPI_index { get; set; }
        public double IPI_index_sum { get; set; }
        public int IPI_infections { get; set; }
        public double UCSC_HTi { get; set; }
        public double UCSC_HT { get; set; }
        public double UCSC_DOR { get; set; }
        public int UCSC_GER { get; set; }
        #endregion

        #region phenology model outputs
        public float chillState { get; set; } //daily chill state
        public float chillRate { get; set; } //daily chill rate
        public float antiChillRate { get; set; } //daily anti-chill rate
        public float forcingState { get; set; } //daily forcing state
        public float forcingRate { get; set; } //daily forcing rate
        public float cycleCompletionPercentage { get; set; } //daily cycle completion percentage
        public float bbchCode { get; set; } //daily BBCH code 
        public float bbchPhase { get; set; } //daily BBCH phase
        public float plantSusceptibility { get; set; } //daily plant susceptibility (0-100)
        #endregion

    }

    #region Classes for specific infection model outputs 
    public class OutputsRule310 
    {
        public List<GenericInfection> infectionEvents = new List<GenericInfection>();
    }
    public class OutputsLaore 
    {
        public List<GenericInfection> infectionEvents = new List<GenericInfection>();

        public double dailyInfectionRisk { get; set; }
    }
    public class OutputsMagarey 
    {
        public List<GenericInfection> infectionEvents = new List<GenericInfection>();

    }
    public class OutputsDMCast 
    {
        public List<GenericInfection> infectionEvents = new List<GenericInfection>();

        public double pomsum { get; set; } 
    }
    public class OutputsEPI 
    {
        public List<GenericInfection> infectionEvents = new List<GenericInfection>();

        public double epi { get; set; }
        public double ke { get; set; }
        public double pe { get; set; }
    }
    public class OutputsMisfits
    {
        public List<GenericInfection> infectionEvents = new List<GenericInfection>();

        public double macrosporangia_suitability_hourly { get; set; }
        public double macrosporangia_suitability_daily { get; set; }
        public double macrosporangia_temperature_function { get; set; }
        public double macrosporangia_temp_moisture_function { get; set; }
        public double macrosporangia_formed_hourly { get; set; }
        public double are_macrosporangia_formed { get; set; }
        public double macrosporangia_spread_hourly { get; set; }
        public double downyMildew_infection_hourly { get; set; }
        public double macrosporangia_infection_temp_moisture_function { get; set; }
        public double are_macrosporangia_spread { get; set; }
        public double is_rained { get; set; }
        public double macrosporangia_precipitation_sum { get; set; }
        public double counter_leafwetness_macrosporangia { get; set; }
        public double macrosporangia_formed_daily { get; set; }
        public double macrosporangia_spread_daily { get; set; }
        public double downyMildew_infection_daily { get; set; }
    }
    public class OutputsIPI 
    {
        public List<GenericInfection> infectionEvents = new List<GenericInfection>();

        public double ipisum { get; set; }

    }
    public class OutputsUCSC 
    {
        public List<GenericInfection> infectionEvents = new List<GenericInfection>();

        public double hti { get; set; }
        public double hts { get; set; }
        public double dor { get; set; }
        public int ger { get; set; }
        

    }
    #endregion

    #region Phenology models outputs
    public class OutputsPhenology 
    {
        // get or set the chill days.
        public float chillState { get; set; }
        public float chillRate { get; set; }
        public float antiChillRate { get; set; }
        public float antiChillState { get; set; }
        public float forcingRate { get; set; }
        public float forcingState { get; set; }
        public float bbchPhenophaseCode { get; set; }
        public float cycleCompletionPercentage { get; set; }
        public float phaseCompletionPercentage { get; set; }
        public int bbchPhenophase { get; set; }
        public int bbchReproductivePhase { get; set; }
        public int bbchVegetativePhase { get; set; }
        public bool isChillStarted { get; set; }
        public float plantSusceptibility { get; set; }
    }
    #endregion
    
    #region Generic infection classes

    //this is the base class, which is implemented by the specific infection class of the models requiring additional outputs
    public class GenericInfection
    {
        #region Generic
        public int idInfection { get; set; }       
        public DateTime infectionDate { get; set; }        
        #endregion
    }

    #region Model specific infection classes
    public class MagareyInfectionEvent : GenericInfection
    {
        #region modelMagarey
        public double TemperatureAverageGerm { get; set; }
        public double RainSumGerm { get; set; }
        public double TemperatureAverageSplash { get; set; }
        public double RainSumSplash { get; set; }
        public double TemperatureAverageInf { get; set; }
        public int GerminationHours { get; set; }
        public int Germination { get; set; }
        public int SplashingHours { get; set; }
        public int Splashing { get; set; }
        public int InfectionHours { get; set; }
        public double DegreeHours { get; set; }
        public double Infection { get; set; }
        public DateTime splashingDate { get; set; }
        public DateTime germinationDate { get; set; }

        #endregion
    }

    public class UCSCInfectionEvent : GenericInfection
    {
        #region modelUCSC
        public double cohortDensity { get; set; } //PMOc
        public double HydroThermalTime { get; set; }
        public DateTime startGermination { get; set; }
        public DateTime endGermination { get; set; }
        public DateTime incubationDate { get; set; }

        public int germination { get; set; } //GER
        public double sporangiaSurvival { get; set; }
        public double germinatedOospores { get; set; }
        public int wetHoursRelease { get; set; }
        public double temperatureSumRel { get; set; }
        public double averageTempWetHoursRel { get; set; }
        public int zoosporeRelease { get; set; }        
        public DateTime zoosporeReleaseDate { get; set; }
        public double releasedZoospores { get; set; }
        public int hoursAfterRelease { get; set; }
        public int wetHoursSurvival { get; set; }
        public double dispersedZoospores { get; set; }
        public int zoosporeDispersal { get; set; }
        public int wetHoursInfection { get; set; }
        public double temperatureSumInf { get; set; }
        public double averageTempWetHoursInf { get; set; }
        public int infection { get; set; }        
        public double zoosporesCausingInfection { get; set; }
        public double infectionCILow { get; set; }
        public double infectionCIUp { get; set; }
        public int incubationPeriod { get; set; }
        



        #endregion
    }

    public class DMcastInfectionEvent : GenericInfection
    {    
        #region modelDMCast
        public double Infection { get; set; }
        public int InfectionHours { get; set; }
        public double RainSumSplash { get; set; }
        public int SporangiaGermination { get; set; }
        public DateTime SporangiaGermDate { get; set; }
        public DateTime germinationDate { get; set; }

        public double TemperatureSum { get; set; }
        public int SporangiaGermHours { get; set; }
        public int PhenoPhase { get; set; }
        public double RainSumInf { get; set; }
       
        #endregion
    }

    public class misfitsInfectionEvent : GenericInfection
    {
        public DateTime germinationDate { get; set; }

    }
    #endregion

    #endregion
}
