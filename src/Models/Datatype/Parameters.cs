//This file contains the classes for the parameters of the models. 
namespace Models.Datatype
{
    //this is the base parameter class, which is implemented in all the parameters classes of specific models
    public class Parameters
    {
        //instance of the phenology parameters
        public parametersPhenology phenologyParameters = new parametersPhenology();
        //dictionary of the BBCH parameters
        public Dictionary<int, parametersBBCH> bbchParameters = new Dictionary<int, parametersBBCH>();
        public Dictionary<int, parametersSusceptibility> bbchSusceptibilityParameters = new Dictionary<int, parametersSusceptibility>();
        //instance of the infection models parameters
        public parametersMagarey magareyParameters = new parametersMagarey();
        public parametersDMCast dmcastParameters = new parametersDMCast();
        public parametersIPI ipiParameters = new parametersIPI();
        public parametersEPI epiParameters = new parametersEPI();
        public parametersRule310 rule310Parameters = new parametersRule310();
        public parametersUCSC ucscParameters = new parametersUCSC();
        public parametersMisfits misfitsParameters = new parametersMisfits();
        public parametersLaore laoreParameters = new parametersLaore();
    }

    #region specific class for infection model parameters

    public class parametersMagarey 
    {
        public float baseTemperatureRule210 { get; set; }
        public float precipitationThresholdRule210 { get; set; }
        public float baseTemperatureInfection { get; set; }
        public float precipitationThresholdInfection { get; set; }
        public float numberOfHoursToConsiderRule210 { get; set; }
        public float numberOfHoursToConsiderGermination { get; set; }
        public float numberOfHoursToConsiderInfection { get; set; }
        public float degreeHoursThresholdInfection { get; set; }
        public float rainTriggeringSplash { get; set; }

    }
    public class parametersDMCast 
    {
        public float muK1 { get; set; }
        public float sigmaK1 { get; set; }
        public float thresholdPOM { get; set; }
        public float precThresholdOosporeGerm { get; set; }
        public float tempThresholdOosporeGerm { get; set; }
        public float tempThresholdSporangiaGerm { get; set; }
        public float daysForSporangiaGerm { get; set; }
        public float precThresholdInf { get; set; }
        public float tempThresholdInf { get; set; }
        public float bbchThreshold { get; set; }
        public float daysForInfection { get; set; }
    }
    public class parametersIPI 
    {
        public float riUpperThreshold { get; set; }
        public float tmeaniTminThreshold { get; set; }
        public float tmeaniUpperThreshold { get; set; }
        public float lwiLowerThreshold { get; set; }
        public float lwiUpperThreshold { get; set; }
        public float rhiUpperThreshold { get; set; }
        public float ipiTminThreshold { get; set; }
        public float alertThreshold { get; set; }
        public float tempThresholdInf { get; set; }
        public float precThresholdInf { get; set; }
        public float bbchThreshold { get; set; }

    }
    public class parametersEPI 
    {
        public float keConstant { get; set; }
        public float urUpperThreshold { get; set; }
        public float urLowerThreshold { get; set; }
        public float peConstant { get; set; }
        public float alertThreshold { get; set; }
        public float tempThresholdInf { get; set; }
        public float precThresholdInf { get; set; }
        public float bbchThreshold { get; set; }

    }
    public class parametersRule310 
    {
        public float baseTemperature { get; set;}
        public float precipitationThreshold { get; set; }
        public float numberOfHoursToConsider { get; set; }
        public float bbchThreshold { get; set; }
    }
    public class parametersUCSC 
    {
        public float vpdThreshold { get; set; }
        public float dormancyBrakingLowerThreshold { get; set; }
        public float dormancyBrakingUpperThreshold { get; set; }
        public float germinationThreshold { get; set; }
        public float sporangiaSurvivalThreshold { get; set; }
        public float zoosporeSurvivalThreshold { get; set; }
        public float infectionThreshold { get; set; }
        public float incubationLowerThreshold { get; set; }
        public float incubationUpperThreshold { get; set; }
    }
    public class parametersMisfits 
    {
        public float LWoptMacrosporangiaFormation { get; set; }
        public float LWminMacrosporangiaFormation { get; set; }
        public float TminMacrosporangiaInfection { get; set; }
        public float ToptMacrosporangiaInfection { get; set; }
        public float LWminMacrosporangiaInfection { get; set; }
        public float TmaxMacrosporangiaInfection { get; set; }
        public float RainStartMacrosporangiaSpread { get; set; }
        public float RHminMacrosporangiaFormation { get; set; }
        public float TmaxMacrosporangiaFormation { get; set; }
        public float TminMacrosporangiaFormation { get; set; }
        public float ToptMacrosporangiaFormation { get; set; }

    }
    public class parametersLaore 
    {
        public float infectionThresholdRisk { get; set; }
    }
    #endregion

    #region phenology models parameters
    //phenology parameters
    public class parametersPhenology 
    {
        public float TminPlant { get; set; } //°C
        public float ToptPlant { get; set; } //°C
        public float TmaxPlant { get; set; }//°C
        public float ChillThreshold { get; set; }//chilling hours
        public float ChillingRequirement { get; set; } //chilling hours
        public float CycleLength { get; set; } //days
      
    }
    //BBCH parameters
    public class parametersBBCH
    {
        public float cycleCompletion { get; set; }        
    }
    //BBCH susceptibility parameters
    public class parametersSusceptibility
    {
        public float susceptibility { get; set; }
    }
    

    #endregion

}
