using System.Reflection;
using Models.Datatype;
using Models.Infections;
using Models.Phenology;
using System.Text;
using octoPusAI.Readers;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Collections.Generic;
using System.Diagnostics;

namespace octoPusAI.ModelCallers
{
    //this class calls the octoPus models, the RandomForest model and the LLama model
    internal class octoPusRunner : UNIMI.optimizer.IOBJfunc //implements the optimizer interface
    {
        #region optimizer methods
        int _neval = 0;
        int _ncompute = 0;

        public Dictionary<int, string> _Phenology = new Dictionary<int, string>();

        // the number of times that this function is called        
        public int neval
        {
            get
            {
                return _neval;
            }

            set
            {
                _neval = value;
            }
        }

        // the number of times where the function is evaluated 
        // (when an evaluation is requested outside the parameters domain this counter is not incremented        
        public int ncompute
        {
            get
            {
                return _ncompute;
            }

            set
            {
                _ncompute = value;
            }
        }
        #endregion

        #region instances of phenology models (the eyes)
        Dormancy chilling = new Dormancy();
        Forcing forcing = new Forcing();
        #endregion

        #region instances of disease models (the tentacles)
        //instance of the infection models
        Rule310 rule310 = new Rule310();
        Magarey magarey = new Magarey();
        EPI epi = new EPI();
        IPI ipi = new IPI();
        Laore laore = new Laore();
        Misfits misfits = new Misfits();
        UCSC ucsc = new UCSC();
        DMCast dmcast = new DMCast();
        Output output = new Output();

        #region internal variables for disease pressure (cumulated infection)
        int pressureRule310 = 0;
        int pressureEPI = 0;
        int pressureIPI = 0;
        int pressureDMCast = 0;
        int pressureMagarey = 0;
        int pressureUCSC = 0;
        int pressureMisfits = 0;
        int pressureLaore = 0;
        #endregion

        #endregion

        #region instance of the weather reader class
        WeatherReader weatherReader = new WeatherReader();
        #endregion

        #region local variables 
        public Dictionary<string, float> octoPusParameters = new Dictionary<string, float>();
        public Dictionary<int, parametersSusceptibility> BBCH_Susceptibility = new Dictionary<int, parametersSusceptibility>();
        public Dictionary<DateTime, OutputsDaily> date_outputs = new Dictionary<DateTime, OutputsDaily>();
        public Dictionary<string, Dictionary<string, float>> site_phenoParam_value = new Dictionary<string, Dictionary<string, float>>();
        public string weatherFile;
        public string cluster;
        public int startYear;
        public int endYear; 
        public float assistantRisk;
        public int veryHighModelsThreshold;
        public string Rversion;
        public string modelPath;
        public string WeatherTimeStep;
        public string weatherDir;
        public bool areEPIDMCASTexecutable;
        public bool useLLM;
        public bool useRandomForest;
        public bool useConsole;
        public bool detailedRun;
        public string calibrationVariable;
        public List<string> availableSites = new List<string>();
        public Dictionary<string, Dictionary<DateTime, Input>> weatherData = new Dictionary<string, Dictionary<DateTime, Input>>();
        public Dictionary<string, ParameterRange> nameParam = new Dictionary<string, ParameterRange>();
        public string modelUnderOptimization;
        public Dictionary<string, float> param_outCalibration = new Dictionary<string, float>();
        public Dictionary<string, Dictionary<int, DateTime>> site_year_onsetDate = new Dictionary<string, Dictionary<int, DateTime>>();
        public Dictionary<int, Dictionary<int, DateTime>> Year_bbchDate_Ref =
            new Dictionary<int, Dictionary<int, DateTime>>();
        #endregion

        #region local variables to compute daily data
        //list of hourly variables to compute daily data
        List<double> Temperatures = new List<double>();
        List<double> Precipitation = new List<double>();
        List<double> RelativeHumidity = new List<double>();
        List<double> Leafwetness = new List<double>();
        List<double> EPI_ke = new List<double>();
        List<double> EPI_pe = new List<double>();
        List<double> DMCast_Pom = new List<double>();
        List<double> DMCast_Ra = new List<double>();
        List<double> IPI_Ri = new List<double>();
        List<double> IPI_Tmeani = new List<double>();
        List<double> IPI_Lwi = new List<double>();
        List<double> IPI_Rhi = new List<double>();
        List<double> IPI_index = new List<double>();
        List<double> UCSC_HTi = new List<double>();
        List<double> UCSC_DOR = new List<double>();
        List<int> UCSC_GER = new List<int>();
        #endregion

        //this is the main call method of the octoPus model
        //public void octoPus(out Dictionary<DateTime, OutputsDaily> date_outputs)
        public double ObjfuncVal(double[] Coefficient, double[,] limits)
        {
            #region Calibration methods
            for (int j = 0; j < Coefficient.Length && j < limits.GetLength(0); j++)
            {
                if (calibrationVariable != "Phenology" && Coefficient[j] == 0)
                {
                    break;
                }
                if (Coefficient[j] < limits[j, 0] | Coefficient[j] > limits[j, 1])
                {
                    return 1E+300;
                }
            }
            _neval++;
            _ncompute++;
            #endregion

            //reinitialize the date_outputs object
            date_outputs = new Dictionary<DateTime, OutputsDaily>();

            #region assign parameters

            #region instance of the parameters class
            Parameters parameters = new Parameters();
            parametersRule310 parRule310 = new parametersRule310();
            PropertyInfo[] propsRule310 = parRule310.GetType().GetProperties();//get all properties
            parametersMagarey parMagarey = new parametersMagarey();
            PropertyInfo[] propsMagarey = parMagarey.GetType().GetProperties();//get all properties
            parametersEPI parEPI = new parametersEPI();
            PropertyInfo[] propsEPI = parEPI.GetType().GetProperties();//get all properties
            parametersIPI parIPI = new parametersIPI();
            PropertyInfo[] propsIPI = parIPI.GetType().GetProperties();//get all properties
            parametersLaore parLaore = new parametersLaore();
            PropertyInfo[] propsLaore = parLaore.GetType().GetProperties();//get all properties
            parametersMisfits parMisfits = new parametersMisfits();
            PropertyInfo[] propsMisfits = parMisfits.GetType().GetProperties();//get all properties
            parametersUCSC parUCSC = new parametersUCSC();
            PropertyInfo[] propsUCSC = parUCSC.GetType().GetProperties();//get all properties
            parametersDMCast parDMCast = new parametersDMCast();
            PropertyInfo[] propsDMCast = parDMCast.GetType().GetProperties();//get all properties
            parametersPhenology parPhenology = new parametersPhenology();
            PropertyInfo[] propsPhenology = parPhenology.GetType().GetProperties();//get all properties 
            parametersBBCH parametersBBCH = new parametersBBCH();
            PropertyInfo[] propsBBCH = parametersBBCH.GetType().GetProperties();//get all properties
            parametersIncubation parametersIncubation = new parametersIncubation();
            PropertyInfo[] propsIncubation = parametersIncubation.GetType().GetProperties();
            #endregion

            int i = 0;

            //assign calibrated parameters
            foreach (var param in nameParam.Keys)
            {
                //split class from param name
                string[] paramClass = param.Split('_');
                string propertyName = param;
                bool isCalibrated = nameParam[param].calibration != "";

                if (modelUnderOptimization == "Rule310")
                {
                    var prop = propsRule310.FirstOrDefault(p => p.Name == propertyName);
                    if (prop != null)
                        prop.SetValue(parRule310, isCalibrated ? (float)Coefficient[i++] : param_outCalibration[param]);
                }
                if (modelUnderOptimization == "Magarey")
                {
                    var prop = propsMagarey.FirstOrDefault(p => p.Name == propertyName);
                    if (prop != null)
                        prop.SetValue(parMagarey, isCalibrated ? (float)Coefficient[i++] : param_outCalibration[param]);
                }
                if (modelUnderOptimization == "EPI")
                {
                    var prop = propsEPI.FirstOrDefault(p => p.Name == propertyName);
                    if (prop != null)
                        prop.SetValue(parEPI, isCalibrated ? (float)Coefficient[i++] : param_outCalibration[param]);
                }
                if (modelUnderOptimization == "IPI")
                {
                    var prop = propsIPI.FirstOrDefault(p => p.Name == propertyName);
                    if (prop != null)
                        prop.SetValue(parIPI, isCalibrated ? (float)Coefficient[i++] : param_outCalibration[param]);
                }
                if (modelUnderOptimization == "Laore")
                {
                    var prop = propsLaore.FirstOrDefault(p => p.Name == propertyName);
                    if (prop != null)
                        prop.SetValue(parLaore, isCalibrated ? (float)Coefficient[i++] : param_outCalibration[param]);
                }
                if (modelUnderOptimization == "Misfits")
                {
                    var prop = propsMisfits.FirstOrDefault(p => p.Name == propertyName);
                    if (prop != null)
                        prop.SetValue(parMisfits, isCalibrated ? (float)Coefficient[i++] : param_outCalibration[param]);
                }
                if (modelUnderOptimization == "UCSC")
                {
                    var prop = propsUCSC.FirstOrDefault(p => p.Name == propertyName);
                    if (prop != null)
                        prop.SetValue(parUCSC, isCalibrated ? (float)Coefficient[i++] : param_outCalibration[param]);
                }
                if (modelUnderOptimization == "DMCast")
                {
                    var prop = propsDMCast.FirstOrDefault(p => p.Name == propertyName);
                    if (prop != null)
                        prop.SetValue(parDMCast, isCalibrated ? (float)Coefficient[i++] : param_outCalibration[param]);
                }
                if (calibrationVariable == "Phenology")
                {
                    if (!propertyName.Contains("bbch"))
                    {
                        // parametri Phenology puri: ChillingRequirement, CycleLength, ecc.
                        var prop = propsPhenology.FirstOrDefault(p => p.Name == propertyName);
                        if (prop != null)
                            prop.SetValue(parPhenology, isCalibrated ? (float)Coefficient[i++] : param_outCalibration[param]);
                    }
                    else
                    {
                        // parametri BBCH: sovrascrive sempre ad ogni iterazione
                        int bbchCode = int.Parse(propertyName.Substring(4, 2));
                        parameters.bbchParameters[bbchCode] = new parametersBBCH();
                        parameters.bbchParameters[bbchCode].cycleCompletion =
                            isCalibrated ? (float)Coefficient[i++] : param_outCalibration[param];
                    }
                }
                if (calibrationVariable != "Phenology")
                {
                    var prop = propsIncubation.FirstOrDefault(p => p.Name == propertyName);
                    if (prop != null)
                        prop.SetValue(parametersIncubation, isCalibrated ? (float)Coefficient[i++] : param_outCalibration[param]);
                }
                
            }

            foreach (var paramPheno in octoPusParameters)
            {
                var paramClass = paramPheno.Key.Split('_');

                if (paramClass[0] == "Phenology")
                {
                    var prop = propsPhenology.FirstOrDefault(p => p.Name == paramClass[1]);
                    if (prop != null)
                        prop.SetValue(parPhenology, paramPheno.Value);
                }
                //if (paramClass[0] == "BBCH")
                //{ 
                //    var prop = propsBBCH.FirstOrDefault(p => p.Name == paramClass[1]);

                //        parametersBBCH = new parametersBBCH();
                //    if (!parameters.bbchParameters.ContainsKey(int.Parse(paramClass[1].Substring(4, 2))))
                //    {

                //        parameters.bbchParameters.Add(int.Parse(paramClass[1].Substring(4, 2)), parametersBBCH);
                //        if (parameters.bbchParameters[int.Parse(paramClass[1].Substring(4, 2))].
                //            cycleCompletion == 0)
                //        {
                //            parameters.bbchParameters[int.Parse(paramClass[1].Substring(4, 2))].cycleCompletion =
                //                (float)(paramPheno.Value); //set the values for this parameter
                //        }
                //    }
                //}
                if (paramClass[0] == "Incubation")
                {
                    if (paramClass[1] != "incubationDuration")
                    {
                        var prop = propsIncubation.FirstOrDefault(p => p.Name == paramClass[1]);
                        if (prop != null)
                            prop.SetValue(parametersIncubation, paramPheno.Value);

                    }

                }
            
            }

            parameters.ucscParameters = parUCSC;
            parameters.misfitsParameters = parMisfits;
            parameters.laoreParameters = parLaore;
            parameters.ipiParameters = parIPI;
            parameters.epiParameters = parEPI;
            parameters.magareyParameters = parMagarey;
            parameters.rule310Parameters = parRule310;
            parameters.dmcastParameters = parDMCast;
            parameters.phenologyParameters = parPhenology;
            #endregion

            #region assign phenology parameters for detailed crop parameters estimation
            parameters.bbchParameters = parameters.bbchParameters.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value); ;

            // NEW: salva gli anchor BBCH calibrati prima che generateDetailed li espanda
            var calibratedBbchAnchors = parameters.bbchParameters
                .ToDictionary(kvp => kvp.Key,
                              kvp => new parametersBBCH { cycleCompletion = kvp.Value.cycleCompletion });

            Parameters _detailedCropParameters = generateDetailedPhenologyParameters(parameters);
            parameters = _detailedCropParameters;
            parameters.bbchSusceptibilityParameters = BBCH_Susceptibility;
            parameters.incubationParameters = parametersIncubation;
            #endregion

            //the error lists
            List<float> errors = new List<float>();
            Dictionary<int, Dictionary<int, DateTime>> SimulatedBBCH_date = new Dictionary<int, Dictionary<int, DateTime>>();


            foreach (var site in availableSites)
            {
               
                //reinitialize the models (for internal lists)
                rule310 = new Rule310();
                misfits = new Misfits();
                ucsc = new UCSC();
                dmcast = new DMCast();
                laore = new Laore();
                ipi = new IPI();
                epi = new EPI();
                magarey = new Magarey();

                //take the reference data for this site (only for onset/epi calibration)
                Dictionary<int, DateTime> year_onsetDate = new Dictionary<int, DateTime>();
                if (calibrationVariable != "Phenology")
                {
                    year_onsetDate = site_year_onsetDate[site];

                    //adjust simulation period
                    if (modelUnderOptimization == "EPI" ||
                        modelUnderOptimization == "DMCast" ||
                        modelUnderOptimization == "UCSC")
                    {
                        startYear = year_onsetDate.Keys.First() - 10;
                        endYear = year_onsetDate.Keys.Last();
                    }
                }




                if (areEPIDMCASTexecutable && 
                    (modelUnderOptimization == "EPI" || modelUnderOptimization == "DMCast"))
                {
                    //for the PEMs that require climatic averages
                    epi = new EPI();
                    dmcast = new DMCast();
                    historicalRun(weatherData[site]);
                }

                bool isFlowered = false;

                //reinitialize the date_outputs object
                date_outputs = new Dictionary<DateTime, OutputsDaily>();

                //initialize the daily outputs object
                OutputsDaily outputsDaily = new OutputsDaily();
                //reinitialize variables for each site
                var outputs = new Output();

                //check if this year has already been evaluated
                bool isAlreadyEvaluated = false;

                //assign calibrated parameters
                parameters.bbchParameters = new Dictionary<int, parametersBBCH>();

                if (calibrationVariable != "Phenology")
                {
                    if (site_phenoParam_value.ContainsKey(site))
                    {
                        parameters.phenologyParameters.ChillingRequirement =
                            site_phenoParam_value[site]["ChillingRequirement"];
                        parameters.phenologyParameters.CycleLength =
                           site_phenoParam_value[site]["CycleLength"];

                        parametersBBCH par = new parametersBBCH();
                        par.cycleCompletion = site_phenoParam_value[site]["bbch08"];
                        parameters.bbchParameters.Add(8, par);

                        par = new parametersBBCH();
                        par.cycleCompletion = site_phenoParam_value[site]["bbch10"];
                        parameters.bbchParameters.Add(10, par);

                        par = new parametersBBCH();
                        par.cycleCompletion = site_phenoParam_value[site]["bbch11"];
                        parameters.bbchParameters.Add(11, par);

                        par = new parametersBBCH();
                        par.cycleCompletion = site_phenoParam_value[site]["bbch53"];
                        parameters.bbchParameters.Add(53, par);

                        par = new parametersBBCH();
                        par.cycleCompletion = site_phenoParam_value[site]["bbch65"];
                        parameters.bbchParameters.Add(65, par);
                    }
                    else//global parameters
                    {
                        parameters.phenologyParameters.ChillingRequirement =
                           site_phenoParam_value["global"]["ChillingRequirement"];
                        parameters.phenologyParameters.CycleLength =
                       site_phenoParam_value["global"]["CycleLength"];

                        parametersBBCH par = new parametersBBCH();
                        par.cycleCompletion = site_phenoParam_value["global"]["bbch08"];
                        parameters.bbchParameters.Add(8, par);

                        par = new parametersBBCH();
                        par.cycleCompletion = site_phenoParam_value["global"]["bbch10"];
                        parameters.bbchParameters.Add(10, par);

                        par = new parametersBBCH();
                        par.cycleCompletion = site_phenoParam_value["global"]["bbch11"];
                        parameters.bbchParameters.Add(11, par);

                        par = new parametersBBCH();
                        par.cycleCompletion = site_phenoParam_value["global"]["bbch53"];
                        parameters.bbchParameters.Add(53, par);

                        par = new parametersBBCH();
                        par.cycleCompletion = site_phenoParam_value["global"]["bbch65"];
                        parameters.bbchParameters.Add(65, par);
                    }
                }
                else
                {
                    // NEW: ripristina gli anchor BBCH calibrati dall'ottimizzatore,
                    // altrimenti bbchParameters resta vuoto e il modello non raggiunge mai gli stadi 8-15
                    foreach (var kvp in calibratedBbchAnchors)
                        parameters.bbchParameters[kvp.Key] =
                            new parametersBBCH { cycleCompletion = kvp.Value.cycleCompletion };
                }
                // Monotonicita' BBCH (solo fenologia): forza ogni anchor >= al precedente
                if (calibrationVariable == "Phenology")
                {
                    var monoCodes = parameters.bbchParameters.Keys.OrderBy(c => c).ToList();
                    for (int m = 1; m < monoCodes.Count; m++)
                    {
                        if (parameters.bbchParameters[monoCodes[m]].cycleCompletion < parameters.bbchParameters[monoCodes[m - 1]].cycleCompletion)
                        {
                            parameters.bbchParameters[monoCodes[m]].cycleCompletion = parameters.bbchParameters[monoCodes[m - 1]].cycleCompletion;
                        }
                    }
                }

                _detailedCropParameters = generateDetailedPhenologyParameters(parameters);
                parameters = _detailedCropParameters;
                parameters.bbchSusceptibilityParameters = BBCH_Susceptibility;
                parameters.incubationParameters = parametersIncubation;


                var x = weatherData[site].Keys.Last();

                //loop over dates

                foreach (var hour in weatherData[site].Keys)
                {
                    // Se vuoi "qualsiasi ora" del 1/1/2023
                   
                    if (hour.Year >= startYear)
                    {
                        if (hour.DayOfYear == 1)
                        {
                            isAlreadyEvaluated = false;

                        }

                        //call the octoPus model
                        modelCall(weatherData[site][hour], parameters, isFlowered, outputs, modelUnderOptimization, out outputsDaily);

                        //add weather data to output object
                        output.weatherInputHourly.Temperature = weatherData[site][hour].Temperature;
                        output.weatherInputHourly.Precipitation = weatherData[site][hour].Precipitation;
                        output.weatherInputHourly.RelativeHumidity = weatherData[site][hour].RelativeHumidity;
                        output.weatherInputHourly.LeafWetness = weatherData[site][hour].LeafWetness;

                        //add the object to the output dictionary
                        if (hour.Hour == 0)
                        {
                            date_outputs.Add(hour, outputsDaily);
                        }

                        if (calibrationVariable == "Phenology")
                        {
                            //if dictionary does not contain the year key
                            if (!SimulatedBBCH_date.ContainsKey(hour.Year))
                            {
                                //add it
                                SimulatedBBCH_date.Add(hour.Year, new Dictionary<int, DateTime>());
                            }

                            //year is certainly present see above
                            //if this year this bbch is not yet added
                            if (!SimulatedBBCH_date[hour.Year].ContainsKey((int)outputsDaily.bbchPhase))
                            {
                                //add it
                                SimulatedBBCH_date[hour.Year].Add((int)outputsDaily.bbchPhase, hour);
                            }
                        }
                        else
                        {
                            //check if the reference data contains the current year
                            if (year_onsetDate.ContainsKey(hour.Year))
                            {
                                //Rule 310
                                if (modelUnderOptimization == "Rule310")
                                {
                                    var simOnsetDate = new DateTime();
                                    if (outputs.outputsRule310.infectionEvents.Count >= 1)
                                    {
                                        if (outputs.outputsRule310.infectionEvents[0].onsetDate.Year > 1)
                                        {
                                            if (!isAlreadyEvaluated)
                                            {
                                                //take the onset date
                                                simOnsetDate = outputs.outputsRule310.infectionEvents[0].onsetDate;
                                                //compute the error
                                                var thisYearError = (simOnsetDate - year_onsetDate[hour.Year]).Days;
                                                errors.Add((float)Math.Pow(thisYearError, 2));
                                                isAlreadyEvaluated = true;
                                            }
                                        }
                                    }
                                }
                                //Laore
                                if (modelUnderOptimization == "Laore")
                                {
                                    var simOnsetDate = new DateTime();
                                    if (outputs.outputsLaore.infectionEvents.Count >= 1)
                                    {
                                        if (outputs.outputsLaore.infectionEvents[0].onsetDate.Year > 1)
                                        {
                                            if (!isAlreadyEvaluated)
                                            {
                                                //take the onset date
                                                simOnsetDate = outputs.outputsLaore.infectionEvents[0].onsetDate;
                                                //compute the error
                                                var thisYearError = (simOnsetDate - year_onsetDate[hour.Year]).Days;
                                                errors.Add((float)Math.Pow(thisYearError, 2));
                                                isAlreadyEvaluated = true;
                                            }
                                        }
                                    }
                                }
                                //EPI
                                if (modelUnderOptimization == "EPI")
                                {
                                    var simOnsetDate = new DateTime();
                                    if (outputs.outputsEPI.infectionEvents.Count >= 1)
                                    {
                                        if (outputs.outputsEPI.infectionEvents[0].onsetDate.Year > 1)
                                        {
                                            if (!isAlreadyEvaluated)
                                            {
                                                //take the onset date
                                                simOnsetDate = outputs.outputsEPI.infectionEvents[0].onsetDate;
                                                //compute the error
                                                var thisYearError = (simOnsetDate - year_onsetDate[hour.Year]).Days;
                                                errors.Add((float)Math.Pow(thisYearError, 2));
                                                isAlreadyEvaluated = true;
                                            }
                                        }
                                    }
                                }
                                //IPI
                                if (modelUnderOptimization == "IPI")
                                {
                                    var simOnsetDate = new DateTime();
                                    if (outputs.outputsIPI.infectionEvents.Count >= 1)
                                    {
                                        if (outputs.outputsIPI.infectionEvents[0].onsetDate.Year > 1)
                                        {
                                            if (!isAlreadyEvaluated)
                                            {
                                                //take the onset date
                                                simOnsetDate = outputs.outputsIPI.infectionEvents[0].onsetDate;
                                                //compute the error
                                                var thisYearError = (simOnsetDate - year_onsetDate[hour.Year]).Days;
                                                errors.Add((float)Math.Pow(thisYearError, 2));
                                                isAlreadyEvaluated = true;
                                            }
                                        }
                                    }
                                }
                                //DMCast
                                if (modelUnderOptimization == "DMCast")
                                {
                                    var simOnsetDate = new DateTime();
                                    if (outputs.outputsDMCast.infectionEvents.Count >= 1)
                                    {
                                        if (outputs.outputsDMCast.infectionEvents[0].onsetDate.Year > 1)
                                        {
                                            if (!isAlreadyEvaluated)
                                            {
                                                //take the onset date
                                                simOnsetDate = outputs.outputsDMCast.infectionEvents[0].onsetDate;
                                                //compute the error
                                                var thisYearError = (simOnsetDate - year_onsetDate[hour.Year]).Days;
                                                errors.Add((float)Math.Pow(thisYearError, 2));
                                                isAlreadyEvaluated = true;
                                            }
                                        }
                                    }
                                }
                                //Magarey
                                if (modelUnderOptimization == "Magarey")
                                {
                                    var simOnsetDate = new DateTime();
                                    if (outputs.outputsMagarey.infectionEvents.Count >= 1)
                                    {
                                        if (outputs.outputsMagarey.infectionEvents[0].onsetDate.Year > 1)
                                        {
                                            if (!isAlreadyEvaluated)
                                            {
                                                //take the onset date
                                                simOnsetDate = outputs.outputsMagarey.infectionEvents[0].onsetDate;
                                                //compute the error
                                                var thisYearError = (simOnsetDate - year_onsetDate[hour.Year]).Days;
                                                errors.Add((float)Math.Pow(thisYearError, 2));
                                                isAlreadyEvaluated = true;
                                            }
                                        }
                                    }
                                }
                                //Misfits
                                if (modelUnderOptimization == "Misfits")
                                {
                                    var simOnsetDate = new DateTime();
                                    if (outputs.outputsMisfits.infectionEvents.Count >= 1)
                                    {
                                        if (outputs.outputsMisfits.infectionEvents[0].onsetDate.Year > 1)
                                        {
                                            if (!isAlreadyEvaluated)
                                            {
                                                //take the onset date
                                                simOnsetDate = outputs.outputsMisfits.infectionEvents[0].onsetDate;
                                                //compute the error
                                                var thisYearError = (simOnsetDate - year_onsetDate[hour.Year]).Days;
                                                errors.Add((float)Math.Pow(thisYearError, 2));
                                                isAlreadyEvaluated = true;
                                            }
                                        }
                                    }
                                }
                                //UCSC
                                if (modelUnderOptimization == "UCSC")
                                {
                                    var simOnsetDate = new DateTime();
                                    if (outputs.outputsUCSC.infectionEvents.Count >= 1)
                                    {
                                        if (outputs.outputsUCSC.infectionEvents[0].onsetDate.Year > 1)
                                        {
                                            if (!isAlreadyEvaluated)
                                            {
                                                //take the onset date
                                                simOnsetDate = outputs.outputsUCSC.infectionEvents[0].onsetDate;
                                                //compute the error
                                                var thisYearError = (simOnsetDate - year_onsetDate[hour.Year]).Days;
                                                errors.Add((float)Math.Pow(thisYearError, 2));
                                                isAlreadyEvaluated = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                }
                //writeOctoPusOutputs(site, date_outputs);
                //Console.WriteLine("site {0} run", site);
            }

            int totalInnerKeys = site_year_onsetDate.Sum(kvp => kvp.Value.Count);

         
            double objectiveFunction = 0;
            if (calibrationVariable == "Phenology")
            {
                List<double> errorsPhenology = new List<double>();
                foreach (var year in Year_bbchDate_Ref.Keys)
                {
                    foreach (var bbch in Year_bbchDate_Ref[year].Keys)
                    {
                        if (bbch >=8 && bbch <= 15)
                        {
                            if (SimulatedBBCH_date[year].ContainsKey(bbch))
                            {
                                errorsPhenology.Add(Math.Pow(Convert.ToDouble((Year_bbchDate_Ref[year][bbch] -
                                    SimulatedBBCH_date[year][bbch]).Days), 2));
                            }
                            else
                            {
                                errorsPhenology.Add(999);
                            }
                        }
                    }
                }
                objectiveFunction = Math.Sqrt(errorsPhenology.Sum() / errorsPhenology.Count());
            }
            else
            {
                if (errors.Count()!= 0)
                {
                    objectiveFunction = Math.Sqrt(errors.Sum() / errors.Count());
                }
                else
                {
                    objectiveFunction = 999;
                }
                
            }


            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"\rRMSE = {Math.Round(objectiveFunction, 2)} days");

            

            return objectiveFunction;
            
        }

        public void oneShot(Dictionary<string, float> paramValue)
        {

            #region assign parameters

            #region instance of the parameters class
            Parameters parameters = new Parameters();
            parametersRule310 parRule310 = new parametersRule310();
            PropertyInfo[] propsRule310 = parRule310.GetType().GetProperties();//get all properties
            parametersMagarey parMagarey = new parametersMagarey();
            PropertyInfo[] propsMagarey = parMagarey.GetType().GetProperties();//get all properties
            parametersEPI parEPI = new parametersEPI();
            PropertyInfo[] propsEPI = parEPI.GetType().GetProperties();//get all properties
            parametersIPI parIPI = new parametersIPI();
            PropertyInfo[] propsIPI = parIPI.GetType().GetProperties();//get all properties
            parametersLaore parLaore = new parametersLaore();
            PropertyInfo[] propsLaore = parLaore.GetType().GetProperties();//get all properties
            parametersMisfits parMisfits = new parametersMisfits();
            PropertyInfo[] propsMisfits = parMisfits.GetType().GetProperties();//get all properties
            parametersUCSC parUCSC = new parametersUCSC();
            PropertyInfo[] propsUCSC = parUCSC.GetType().GetProperties();//get all properties
            parametersDMCast parDMCast = new parametersDMCast();
            PropertyInfo[] propsDMCast = parDMCast.GetType().GetProperties();//get all properties
            parametersPhenology parPhenology = new parametersPhenology();
            PropertyInfo[] propsPhenology = parPhenology.GetType().GetProperties();//get all properties 
            parametersBBCH parametersBBCH = new parametersBBCH();
            PropertyInfo[] propsBBCH = parametersBBCH.GetType().GetProperties();//get all properties
            parametersIncubation parametersIncubation = new parametersIncubation();
            PropertyInfo[] propsIncubation = parametersIncubation.GetType().GetProperties();
            #endregion

            int i = 0;

            //assign calibrated parameters
            foreach (var param in nameParam.Keys)
            {
                //split class from param name
                string[] paramClass = param.Split('_');
                string propertyName = param;
                bool isCalibrated = nameParam[param].calibration != "";

                if (modelUnderOptimization == "Rule310")
                {
                    var prop = propsRule310.FirstOrDefault(p => p.Name == propertyName);
                    if (prop != null)
                        prop.SetValue(parRule310, isCalibrated ? (float)paramValue[param] : param_outCalibration[param]);
                }
                if (modelUnderOptimization == "Magarey")
                {
                    var prop = propsMagarey.FirstOrDefault(p => p.Name == propertyName);
                    if (prop != null)
                        prop.SetValue(parMagarey, isCalibrated ? (float)paramValue[param] : param_outCalibration[param]);
                }
                if (modelUnderOptimization == "EPI")
                {
                    var prop = propsEPI.FirstOrDefault(p => p.Name == propertyName);
                    if (prop != null)
                        prop.SetValue(parEPI, isCalibrated ? (float)paramValue[param] : param_outCalibration[param]);
                }
                if (modelUnderOptimization == "IPI")
                {
                    var prop = propsIPI.FirstOrDefault(p => p.Name == propertyName);
                    if (prop != null)
                        prop.SetValue(parIPI, isCalibrated ? (float)paramValue[param] : param_outCalibration[param]);
                }
                if (modelUnderOptimization == "Laore")
                {
                    var prop = propsLaore.FirstOrDefault(p => p.Name == propertyName);
                    if (prop != null)
                        prop.SetValue(parLaore, isCalibrated ? (float)paramValue[param] : param_outCalibration[param]);
                }
                if (modelUnderOptimization == "Misfits")
                {
                    var prop = propsMisfits.FirstOrDefault(p => p.Name == propertyName);
                    if (prop != null)
                        prop.SetValue(parMisfits, isCalibrated ? (float)paramValue[param] : param_outCalibration[param]);
                }
                if (modelUnderOptimization == "UCSC")
                {
                    var prop = propsUCSC.FirstOrDefault(p => p.Name == propertyName);
                    if (prop != null)
                        prop.SetValue(parUCSC, isCalibrated ? (float)paramValue[param] : param_outCalibration[param]);
                }
                if (modelUnderOptimization == "DMCast")
                {
                    var prop = propsDMCast.FirstOrDefault(p => p.Name == propertyName);
                    if (prop != null)
                        prop.SetValue(parDMCast, isCalibrated ? (float)paramValue[param] : param_outCalibration[param]);
                }
                if (modelUnderOptimization == "Phenology")
                {
                    var prop = propsPhenology.FirstOrDefault(p => p.Name == propertyName);
                    if (prop != null)
                        prop.SetValue(parPhenology, isCalibrated ? (float)paramValue[param] : param_outCalibration[param]);

                    if (propertyName.Contains("bbch"))
                    {
                        prop = propsBBCH.FirstOrDefault(p => p.Name == "cycleCompletion");

                        parametersBBCH = new parametersBBCH();
                        parameters.bbchParameters.Add(int.Parse(paramClass[0].Substring(4, 2)), parametersBBCH);
                        if (parameters.bbchParameters[int.Parse(paramClass[0].Substring(4, 2))].
                            cycleCompletion == 0)
                        {
                            prop.SetValue(parametersBBCH, isCalibrated ? (float)paramValue[param] : param_outCalibration[param]);
                        }
                    }

                }
          
            }

            foreach (var paramPheno in octoPusParameters)
            {
                var paramClass = paramPheno.Key.Split('_');

                //TODO: check for onset optimization
                if (paramClass[0] == "Phenology")
                {
                    var prop = propsPhenology.FirstOrDefault(p => p.Name == paramClass[1]);
                    if (prop != null)
                        prop.SetValue(parPhenology, paramPheno.Value);
                }
                if (paramClass[0] == "BBCH" && calibrationVariable != "Phenology")
                {
                    var prop = propsBBCH.FirstOrDefault(p => p.Name == paramClass[1]);

                    parametersBBCH = new parametersBBCH();
                    parameters.bbchParameters.Add(int.Parse(paramClass[1].Substring(4, 2)), parametersBBCH);
                    if (parameters.bbchParameters[int.Parse(paramClass[1].Substring(4, 2))].
                        cycleCompletion == 0)
                    {
                        parameters.bbchParameters[int.Parse(paramClass[1].Substring(4, 2))].cycleCompletion =
                            (float)(paramPheno.Value); //set the values for this parameter
                    }
                }
                if (paramClass[0] == "Incubation")
                {
                    if (!paramPheno.Key.Contains("incubationDuration"))
                    {
                        var prop = propsIncubation.FirstOrDefault(p => p.Name == paramClass[1]);
                        if (prop != null)
                            prop.SetValue(parametersIncubation, paramPheno.Value);
                    }
                    else
                    {
                        var prop = propsIncubation.FirstOrDefault(p => p.Name == paramClass[1]);
                        if (prop != null)
                        {
                            // (A) CONDITION: IF YOU WANT CALIBRATE THE INCUBATION DURATION PARAM.
                            //prop.SetValue(parametersIncubation, paramValue["incubationDuration"]); //<-- use paramValue



                            // (B) CONDITION: IF YOU WANT USE THE FIX INCUBATION DURATION PARAM.

                            //(B1) Use a differente value for cluster
                            float fixedIncubationDuration;
                            if (cluster == "C1")
                                fixedIncubationDuration = 9f; //median cluster C1
                            else if (cluster == "C2")
                                fixedIncubationDuration = 17f; //median cluster C2

                            else fixedIncubationDuration = paramPheno.Value; //fallback

                            prop.SetValue(parametersIncubation, fixedIncubationDuration);


                            //(B2)
                            //prop.SetValue(parametersIncubation, paramPheno.Value); // <-- FORCE NON paramValue

                            //check current value of durationIcubation
                            var currentValue = prop.GetValue(parametersIncubation);

                            // DEBUG: WHICH VALUE IS ACTUALLY USED?
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine(
                                $"[DEBUG] incubationDuration USED = {fixedIncubationDuration} (cluster={cluster})");

                            //debug version to check the calibrated value 
                            // $"[DEBUG] incubationDuration USED = {paramValue["incubationDuration"]} (CALIBRATED).
                            Console.ResetColor();
                        }
                    }
                }


            }

            parameters.ucscParameters = parUCSC;
            parameters.misfitsParameters = parMisfits;
            parameters.laoreParameters = parLaore;
            parameters.ipiParameters = parIPI;
            parameters.epiParameters = parEPI;
            parameters.magareyParameters = parMagarey;
            parameters.rule310Parameters = parRule310;
            parameters.dmcastParameters = parDMCast;
            parameters.phenologyParameters = parPhenology;
            parameters.incubationParameters = parametersIncubation;
            #endregion

            #region assign phenology parameters for detailed crop parameters estimation
            parameters.bbchParameters = parameters.bbchParameters.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value); ;

            // NEW: salva gli anchor BBCH calibrati prima che generateDetailed li espanda
            var calibratedBbchAnchors = parameters.bbchParameters
                .ToDictionary(kvp => kvp.Key,
                              kvp => new parametersBBCH { cycleCompletion = kvp.Value.cycleCompletion });

            Parameters _detailedCropParameters = generateDetailedPhenologyParameters(parameters);
            parameters = _detailedCropParameters;
            parameters.bbchSusceptibilityParameters = BBCH_Susceptibility;
            #endregion


            foreach (var site in availableSites)
            {
                //reinitialize the models (for internal lists)
                rule310 = new Rule310();
                misfits = new Misfits();
                ucsc = new UCSC();
                dmcast = new DMCast();
                laore = new Laore();
                ipi = new IPI();
                epi = new EPI();
                magarey = new Magarey();


                ///take the reference data for this site (only for onset/epi calibration)
                Dictionary<int, DateTime> year_onsetDate = new Dictionary<int, DateTime>();
                if (calibrationVariable != "Phenology")
                {
                    year_onsetDate = site_year_onsetDate[site];
                    //adjust simulation period
                    if (modelUnderOptimization == "EPI" ||
                        modelUnderOptimization == "DMCast" ||
                        modelUnderOptimization == "UCSC")
                    {
                        startYear = year_onsetDate.Keys.First() - 10;
                        endYear = year_onsetDate.Keys.Last();
                    }
                }

                //read weather data
                var weatherData = new Dictionary<DateTime, Input>();
                weatherFile = site;
                switch (WeatherTimeStep)
                {
                    case "hourly":
                        weatherData = weatherReader.readHourly(
                        Path.Combine(weatherDir, "weather_station_" + site + ".csv"),
                        startYear, endYear);
                        break;

                    case "daily":
                        Dictionary<DateTime, InputDaily> weatherDataH = weatherReader.readDaily(weatherDir + "\\" + weatherFile + ".csv", startYear, endYear);
                        foreach (var day in weatherDataH.Keys)
                        {
                            weatherData.AddRange(weatherReader.estimateHourly(weatherDataH[day], day));
                        }
                        break;

                    default:
                        Console.WriteLine("Check the WeatherTimeStep in the octoPus.json file, available choices are: \"daily\" or \"hourly\"");
                        break;
                }

                if (areEPIDMCASTexecutable &&
                    (modelUnderOptimization == "EPI" || modelUnderOptimization == "DMCast"))
                {
                    //for the PEMs that require climatic averages
                    epi = new EPI();
                    dmcast = new DMCast();
                    historicalRun(weatherData);
                }

                bool isFlowered = false;

                //reinitialize the date_outputs object
                date_outputs = new Dictionary<DateTime, OutputsDaily>();

                //initialize the daily outputs object
                OutputsDaily outputsDaily = new OutputsDaily();
                //reinitialize variables for each site
                var outputs = new Output();

                //assign calibrated parameters
                parameters.bbchParameters = new Dictionary<int, parametersBBCH>();

                if (calibrationVariable != "Phenology")
                {
                    if (site_phenoParam_value.ContainsKey(site))
                    {
                        parameters.phenologyParameters.ChillingRequirement =
                            site_phenoParam_value[site]["ChillingRequirement"];
                        parameters.phenologyParameters.CycleLength =
                           site_phenoParam_value[site]["CycleLength"];
                        parametersBBCH par = new parametersBBCH();
                        par.cycleCompletion = site_phenoParam_value[site]["bbch08"];
                        parameters.bbchParameters.Add(8, par);
                        par = new parametersBBCH();
                        par.cycleCompletion = site_phenoParam_value[site]["bbch10"];
                        parameters.bbchParameters.Add(10, par);
                        par = new parametersBBCH();
                        par.cycleCompletion = site_phenoParam_value[site]["bbch11"];
                        parameters.bbchParameters.Add(11, par);
                        par = new parametersBBCH();
                        par.cycleCompletion = site_phenoParam_value[site]["bbch53"];
                        parameters.bbchParameters.Add(53, par);
                        par = new parametersBBCH();
                        par.cycleCompletion = site_phenoParam_value[site]["bbch65"];
                        parameters.bbchParameters.Add(65, par);
                    }
                    else //global parameters
                    {
                        parameters.phenologyParameters.ChillingRequirement =
                           site_phenoParam_value["global"]["ChillingRequirement"];
                        parameters.phenologyParameters.CycleLength =
                           site_phenoParam_value["global"]["CycleLength"];
                        parametersBBCH par = new parametersBBCH();
                        par.cycleCompletion = site_phenoParam_value["global"]["bbch08"];
                        parameters.bbchParameters.Add(8, par);
                        par = new parametersBBCH();
                        par.cycleCompletion = site_phenoParam_value["global"]["bbch10"];
                        parameters.bbchParameters.Add(10, par);
                        par = new parametersBBCH();
                        par.cycleCompletion = site_phenoParam_value["global"]["bbch11"];
                        parameters.bbchParameters.Add(11, par);
                        par = new parametersBBCH();
                        par.cycleCompletion = site_phenoParam_value["global"]["bbch53"];
                        parameters.bbchParameters.Add(53, par);
                        par = new parametersBBCH();
                        par.cycleCompletion = site_phenoParam_value["global"]["bbch65"];
                        parameters.bbchParameters.Add(65, par);
                    }
                }
                else
                {
                    foreach (var kvp in calibratedBbchAnchors)
                        parameters.bbchParameters[kvp.Key] =
                            new parametersBBCH { cycleCompletion = kvp.Value.cycleCompletion };
                }

                // Monotonicita' BBCH (solo fenologia): forza ogni anchor >= al precedente
                if (calibrationVariable == "Phenology")
                {
                    var monoCodes = parameters.bbchParameters.Keys.OrderBy(c => c).ToList();
                    for (int m = 1; m < monoCodes.Count; m++)
                    {
                        if (parameters.bbchParameters[monoCodes[m]].cycleCompletion < parameters.bbchParameters[monoCodes[m - 1]].cycleCompletion)
                        {
                            parameters.bbchParameters[monoCodes[m]].cycleCompletion = parameters.bbchParameters[monoCodes[m - 1]].cycleCompletion;
                        }
                    }
                }

                _detailedCropParameters = generateDetailedPhenologyParameters(parameters);
                parameters = _detailedCropParameters;
                parameters.bbchSusceptibilityParameters = BBCH_Susceptibility;
                parameters.incubationParameters = parametersIncubation;



                //loop over dates
                foreach (var hour in weatherData.Keys)
                {

                    //call the octoPus model
                    modelCall(weatherData[hour], parameters, isFlowered, outputs, modelUnderOptimization, out outputsDaily);

                    //add weather data to output object
                    output.weatherInputHourly.Temperature = weatherData[hour].Temperature;
                    output.weatherInputHourly.Precipitation = weatherData[hour].Precipitation;
                    output.weatherInputHourly.RelativeHumidity = weatherData[hour].RelativeHumidity;
                    output.weatherInputHourly.LeafWetness = weatherData[hour].LeafWetness;

                    

                    //add the object to the output dictionary
                    if (hour.Hour == 0)
                    {
                        date_outputs.Add(hour, outputsDaily);
                    }
                }
                writeOctoPusOutputs(weatherFile, date_outputs);

            }            
        }



        //method to write estimated weather data to csv
        public void writeEstimatedToCsv(string site, Dictionary<DateTime, Input> estimated_hourly)
        {
            //empty list to store outputs
            List<string> ToWrite = new List<string>();

            //define the file header
            string Header = "site,Date,temp,prec,RH,lw";
            ToWrite.Add(Header);

            //loop over the rows
            foreach (var value in estimated_hourly.Keys)
            {
                var Line = new StringBuilder();
                Line.Append($"{weatherFile},");
                Line.Append($"{estimated_hourly[value].Date},");
                Line.Append($"{estimated_hourly[value].Temperature},");
                Line.Append($"{estimated_hourly[value].Precipitation},");
                Line.Append($"{estimated_hourly[value].RelativeHumidity},");
                Line.Append($"{estimated_hourly[value].LeafWetness},");

                ToWrite.Add(Line.ToString());
               
            }
            //save the file
            System.IO.File.WriteAllLines(@"outputs//diseaseModels//estimatedHourly.csv", ToWrite);
        }

        public void historicalRun(Dictionary<DateTime, Input> weatherData)
        {
            #region for EPI and DMCAST model, dictionaries to store climatic averages
            //Climatic monthly averages (for EPI)
            Dictionary<int, List<double>> ClimaticMonthlyRainfall = new Dictionary<int, List<double>>();
            Dictionary<int, List<double>> ClimaticMonthlyRainyDays = new Dictionary<int, List<double>>();
            Dictionary<int, List<double>> ClimaticMonthlyTemperature = new Dictionary<int, List<double>>();
            Dictionary<int, List<double>> ClimaticMonthlyRelativeHumidityNight = new Dictionary<int, List<double>>();

            List<Input> ClimaticLast24Hours = new List<Input>();


            // Initialize variables to keep track of rainy days and rainfall sum
            int rainyDays = 0;
            double monthlyRain = 0;

            //Compute long term averages and prediction year
            foreach (Input Input in weatherData.Values)
            {
                #region add key (year) to dictionaries of monthly data
                //if the dictionary does not contain the key, add it
                if (!ClimaticMonthlyRainfall.ContainsKey(Input.Date.Month))
                {
                    //add a key (month) if not present
                    ClimaticMonthlyRainfall.Add(Input.Date.Month, new List<double>());
                    ClimaticMonthlyTemperature.Add(Input.Date.Month, new List<double>());
                    ClimaticMonthlyRainyDays.Add(Input.Date.Month, new List<double>());
                    ClimaticMonthlyRelativeHumidityNight.Add(Input.Date.Month, new List<double>());
                }
                #endregion

                //add variables to the dictionary in the corresponding key
                //ClimaticMonthlyRainfall[Input.Date.Month].Add(Input.Precipitation);
                ClimaticMonthlyTemperature[Input.Date.Month].Add(Input.Temperature);
                if (Input.Date.Hour >= 18 || Input.Date.Hour <= 9)
                {
                    ClimaticMonthlyRelativeHumidityNight[Input.Date.Month].Add(Input.RelativeHumidity);
                }

                //compute rainy days and daily precipitations per month, required for climatic std of monthly rain
                #region climatic monthly rainfall and rainy days
                ClimaticLast24Hours.Add(Input);
                int lastDayOfMonth = DateTime.DaysInMonth(Input.Date.Year, Input.Date.Month);
                if (ClimaticLast24Hours.Count == 23 * lastDayOfMonth)
                {
                    //rainfall sum in the decade
                    double MonthlyRain = ClimaticLast24Hours.Where(x => x.Precipitation > 0.2).
                        Select(x => x.Precipitation).Sum();
                    //number of rainy days
                    int RainyDays = 0;
                    for (int i = 0; i < 23 * lastDayOfMonth; i++)
                    {
                        if (ClimaticLast24Hours[i].Precipitation > 0.2)
                        {
                            RainyDays++;
                            int Hour = 24 - ClimaticLast24Hours[i].Date.Hour;
                            i = i + Hour;
                        }

                    }
                    ClimaticMonthlyRainyDays[Input.Date.Month].Add(RainyDays);
                    ClimaticMonthlyRainfall[Input.Date.Month].Add(MonthlyRain);
                    ClimaticLast24Hours = new List<Input>();
                }
                #endregion

            }


            #endregion

            #region model and parameters instances
            #region EPI
            //assign climatic data
            for (int i = 1; i <= 12; i++)
            {
                epi.ClimaticAverageTemperature.Add(i, ClimaticMonthlyTemperature[i].Average());
                epi.ClimaticRainfallSum.Add(i, ClimaticMonthlyRainfall[i].Average());
                epi.ClimaticRainyDays.Add(i, ClimaticMonthlyRainyDays[i].Average());
                epi.ClimaticRelativeHumidityNight.Add(i, ClimaticMonthlyRelativeHumidityNight[i].Average());

            }
            #endregion

            #region DMCast
            //assign climatic data
            for (int i = 1; i <= 12; i++)
            {
                dmcast.ClimaticRainfallSum.Add(i, ClimaticMonthlyRainfall[i].Average());
                dmcast.ClimaticRainyDays.Add(i, ClimaticMonthlyRainyDays[i].Average());
                dmcast.ClimaticStdRainfallSum.Add(i, ClimaticMonthlyRainfall[i].StandardDeviation());
            }

            #endregion

            #endregion

        }

        #region write output files
        
        //write outputs from the validation run
        public void writeOctoPusOutputs(string site, Dictionary<DateTime, OutputsDaily> date_outputs)
        {

            #region write outputs
            //empty list to store outputs
            List<string> toWrite = new List<string>();

            //define the file header
            string header = "site,Date,Tmax,Tmin,Prec,LW,RHmax,RHmin," +
            // Phenology model outputs
            "chillState,ChillRate,antiChillState," +
            "forcingRate,forcingState,cycleCompletion," +
            "bbchCode,bbchPhase,bbchRef,plantSusceptibility," +
            // Main model outputs
            "Rule310,Epi,Ipi,Dmcast,Magarey,UCSC,misfits,laore," +
            // Onset model outputs
            "onsRule310,onsEpi,onsIpi,onsDmcast,onsMagarey,onsUCSC,onsmisfits,onslaore," +
            // Main model outputs (pressure)
            "pressureRule310,pressureEpi,pressureIpi,pressureDmcast,pressureMagarey," +
            "pressureUCSC,pressureMisfits,pressureLaore";
            

            //add the header to the list
            toWrite.Add(header);


            //loop over days
            foreach (var date in date_outputs.Keys)
            {   
                if (date.Hour == 0)
                {
                    var line = new StringBuilder();
                    line.Append($"{date_outputs[date].Input.Site},");
                    #region Weather data
                    line.Append($"{date.ToShortDateString()},");
                    line.Append($"{date_outputs[date].Input.Tmax},");
                    line.Append($"{date_outputs[date].Input.Tmin},");
                    line.Append($"{date_outputs[date].Input.Precipitation},");
                    line.Append($"{date_outputs[date].Input.LeafWetnessDuration},");
                    line.Append($"{date_outputs[date].Input.RHmax},");
                    line.Append($"{date_outputs[date].Input.RHmin},");
                    #endregion

                    // Phenology
                    line.Append($"{date_outputs[date].chillState},");
                    line.Append($"{date_outputs[date].chillRate},");
                    line.Append($"{date_outputs[date].antiChillRate},");
                    line.Append($"{date_outputs[date].forcingRate},");
                    line.Append($"{date_outputs[date].forcingState},");
                    line.Append($"{date_outputs[date].cycleCompletionPercentage},");
                    line.Append($"{date_outputs[date].bbchCode},");
                    line.Append($"{date_outputs[date].bbchPhase},");

                    // ✅ BBCH Reference (if date matches)
                    string bbchRefValue = "";
                    if (Year_bbchDate_Ref.TryGetValue(date.Year, out var bbchDict))
                    {
                        // Find if the date matches any reference date for that year
                        var match = bbchDict.FirstOrDefault(kv => kv.Value.Date == date.Date);
                        if (match.Value != default(DateTime))
                        {
                            bbchRefValue = match.Key.ToString(); // use the BBCH code as string
                        }
                    }
                    line.Append($"{bbchRefValue},");

                    // Continue with the rest of your outputs
                    #region Main model outputs
                    line.Append($"{date_outputs[date].plantSusceptibility},");
                    line.Append($"{date_outputs[date].infectionRule310},");
                    line.Append($"{date_outputs[date].infectionEPI},");
                    line.Append($"{date_outputs[date].infectionIPI},");
                    line.Append($"{date_outputs[date].infectionDMCast},");
                    line.Append($"{date_outputs[date].infectionMagarey},");
                    line.Append($"{date_outputs[date].infectionUCSC},");
                    line.Append($"{date_outputs[date].infectionMisfits},");
                    line.Append($"{date_outputs[date].infectionLaore},");

                    // Onset outputs
                    line.Append($"{date_outputs[date].onsetRule310},");
                    line.Append($"{date_outputs[date].onsetEPI},");
                    line.Append($"{date_outputs[date].onsetIPI},");
                    line.Append($"{date_outputs[date].onsetDMCast},");
                    line.Append($"{date_outputs[date].onsetMagarey},");
                    line.Append($"{date_outputs[date].onsetUCSC},");
                    line.Append($"{date_outputs[date].onsetMisfits},");
                    line.Append($"{date_outputs[date].onsetLaore},");

                    // Pressure outputs
                    line.Append($"{date_outputs[date].pressureRule310},");
                    line.Append($"{date_outputs[date].pressureEPI},");
                    line.Append($"{date_outputs[date].pressureIPI},");
                    line.Append($"{date_outputs[date].pressureDMCast},");
                    line.Append($"{date_outputs[date].pressureMagarey},");
                    line.Append($"{date_outputs[date].pressureUCSC},");
                    line.Append($"{date_outputs[date].pressureMisfits},");
                    line.Append($"{date_outputs[date].pressureLaore}");
                    #endregion

                    toWrite.Add(line.ToString());
                }
            }

            // Find the last occurrence of the directory separator character
            int lastIndex = site.LastIndexOf('\\');
            string siteShort = site.Substring(lastIndex + 1) + ".csv";
            
            //save the file
            System.IO.File.WriteAllLines(@"outputs//diseaseModels//" + modelUnderOptimization + "_" + cluster + "_" + siteShort , toWrite);
            #endregion

        }

        public void writeOctoPusOutputsDetailed(string site, Dictionary<DateTime, OutputsDaily> date_outputs)
        {

            #region write outputs
            //empty list to store outputs
            List<string> toWrite = new List<string>();

            //define the file header
            string header = "site,Date,Tmax,Tmin,Prec,LW,RHmax,RHmin," +
            // Phenology model outputs
            "chillState,ChillRate,antiChillState," +
            "forcingRate,forcingState,cycleCompletion," +
            "bbchCode,bbchPhase,plantSusceptibility," +
            // Main model outputs
            "Rule310,Epi,Ipi,Dmcast,Magarey,UCSC,misfits,laore," +
            // Main model outputs (pressure)
            "pressureRule310,pressureEpi,pressureIpi,pressureDmcast,pressureMagarey,pressureUCSC,pressureMisfits,pressureLaore," +
            "EPI_Ke,EPI_pe,EPI_index,DMcast_Ra,DMcast_Pom,DMcast_PomSum," +
            "IPI_Tmeani,IPI_Ri,IPI_Rhi,IPI_Lwi,IPI_index,IPI_index_sum," +
            "UCSC_HTi,UCSC_HT,UCSC_DOR,UCSC_GER";

          
            //add the header to the list
            toWrite.Add(header);


            //loop over days
            foreach (var date in date_outputs.Keys)
            {
                if (date.Hour == 00)
                {
                    var line = new StringBuilder();
                    line.Append($"{date_outputs[date].Input.Site},");
                    #region Weather data
                    line.Append($"{date},");
                    line.Append($"{date_outputs[date].Input.Tmax},");
                    line.Append($"{date_outputs[date].Input.Tmin},");
                    line.Append($"{date_outputs[date].Input.Precipitation},");
                    line.Append($"{date_outputs[date].Input.LeafWetnessDuration},");
                    line.Append($"{date_outputs[date].Input.RHmax},");
                    line.Append($"{date_outputs[date].Input.RHmin},");
                    #endregion
                    //phenology
                    line.Append($"{date_outputs[date].chillState},");
                    line.Append($"{date_outputs[date].chillRate},");
                    line.Append($"{date_outputs[date].antiChillRate},");
                    line.Append($"{date_outputs[date].forcingRate},");
                    line.Append($"{date_outputs[date].forcingState},");
                    line.Append($"{date_outputs[date].cycleCompletionPercentage},");
                    line.Append($"{date_outputs[date].bbchCode},");
                    line.Append($"{date_outputs[date].bbchPhase},");

                    #region Main model outputs
                    line.Append($"{date_outputs[date].plantSusceptibility},");
                    line.Append($"{date_outputs[date].infectionRule310},");
                    line.Append($"{date_outputs[date].infectionEPI},");
                    line.Append($"{date_outputs[date].infectionIPI},");
                    line.Append($"{date_outputs[date].infectionDMCast},");
                    line.Append($"{date_outputs[date].infectionMagarey},");
                    line.Append($"{date_outputs[date].infectionUCSC},");
                    line.Append($"{date_outputs[date].infectionMisfits},");
                    line.Append($"{date_outputs[date].infectionLaore},");

                    line.Append($"{date_outputs[date].pressureRule310},");
                    line.Append($"{date_outputs[date].pressureEPI},");
                    line.Append($"{date_outputs[date].pressureIPI},");
                    line.Append($"{date_outputs[date].pressureDMCast},");
                    line.Append($"{date_outputs[date].pressureMagarey},");
                    line.Append($"{date_outputs[date].pressureUCSC},");
                    line.Append($"{date_outputs[date].pressureMisfits},");
                    line.Append($"{date_outputs[date].pressureLaore},");
                    #endregion

                    #region Model suboutputs
                    // EPI
                    line.Append($"{date_outputs[date].EPI_ke},");
                    line.Append($"{date_outputs[date].EPI_pe},");
                    line.Append($"{date_outputs[date].EPI_index},");
                    // DMCast      date_outputs[date]
                    line.Append($"{date_outputs[date].DMCast_Ra},");
                    line.Append($"{date_outputs[date].DMCast_Pom},");
                    line.Append($"{date_outputs[date].DMCast_PomSum},");
                    // IPI         date_outputs[date]
                    line.Append($"{date_outputs[date].IPI_Tmeani},");
                    line.Append($"{date_outputs[date].IPI_Ri},");
                    line.Append($"{date_outputs[date].IPI_Rhi},");
                    line.Append($"{date_outputs[date].IPI_Lwi},");
                    line.Append($"{date_outputs[date].IPI_index},");
                    line.Append($"{date_outputs[date].IPI_index_sum},");
                    // UCSC       date_outputs[date]
                    line.Append($"{date_outputs[date].UCSC_HTi},");
                    line.Append($"{date_outputs[date].UCSC_HT},");
                    line.Append($"{date_outputs[date].UCSC_DOR},");
                    line.Append($"{date_outputs[date].UCSC_GER}");                  
                    #endregion

                    toWrite.Add(line.ToString());

                }
            }
            // Find the last occurrence of the directory separator character
            int lastIndex = site.LastIndexOf('\\');
            string siteShort = site.Substring(lastIndex + 1);

            //save the file
            System.IO.File.WriteAllLines(@"outputs//diseaseModels//detailed_" + siteShort, toWrite);
            #endregion

        }
        #endregion

        // Execute a single hourly timestep
        public void modelCall(Input weatherData, Parameters parameters, bool isFlowered, Output outputs,
            string modelUnderOptimization, out OutputsDaily modelsOutput)
        {
            //initialize the daily output object
            modelsOutput = new OutputsDaily();

            //add weather data to the lists
            Temperatures.Add(weatherData.Temperature);
            Precipitation.Add(weatherData.Precipitation);
            RelativeHumidity.Add(weatherData.RelativeHumidity);
            Leafwetness.Add(weatherData.LeafWetness);

            //reinizialize objects at the start of the year
            if (weatherData.Date.DayOfYear == 1)
            {
                isFlowered = false;
                outputs.outputsMagarey = new OutputsMagarey();
                //reinitialize the models at first day of the year
                laore = new Laore();
                misfits = new Misfits();
                ipi = new IPI();

                #region reinitialize pressure
                pressureRule310 = 0;
                pressureEPI = 0;
                pressureIPI = 0;
                pressureDMCast = 0;
                pressureMagarey = 0;
                pressureUCSC = 0;
                pressureMisfits = 0;
                pressureLaore = 0;
                #endregion

            }

            //reinitialize outputsPhenology each year
            if (weatherData.Date.DayOfYear == 300 && weatherData.Date.Hour == 0)
            {
                outputs.outputsPhenology = new OutputsPhenology();

                if (modelUnderOptimization == "EPI")
                {
                    //reinitialize the UCSC model at the start of the season
                    epi.MonthlyCounts = new List<Input>();
                    epi.DecadeCounts = new List<Input>();
                    epi.KeCounts = new List<Input>();
                    epi.InfectionCount = new List<Input>();
                }
                //if (modelUnderOptimization == "UCSC")
                //{
                //    //ucsc = new UCSC();

                //}

                //clean infection lists from previous year
                outputs.outputsEPI.infectionEvents = new List<GenericInfection>();
                outputs.outputsIPI.infectionEvents = new List<GenericInfection>();
                outputs.outputsDMCast.infectionEvents = new List<GenericInfection>();
                outputs.outputsLaore.infectionEvents = new List<GenericInfection>();
                outputs.outputsMagarey.infectionEvents = new List<GenericInfection>();
                outputs.outputsMisfits.infectionEvents = new List<GenericInfection>();
                outputs.outputsUCSC.infectionEvents = new List<GenericInfection>();
                outputs.outputsRule310.infectionEvents = new List<GenericInfection>();

            }

            if (modelUnderOptimization == "Magarey")
            {
                //call the octoPus models
                magarey.run(weatherData, parameters, outputs);
            } else if (modelUnderOptimization == "EPI")
            {
                epi.run(weatherData, parameters, outputs);
            }
            else if (modelUnderOptimization == "DMCast")
            {
                dmcast.run(weatherData, parameters, outputs);
            }
            else if (modelUnderOptimization == "UCSC")
            {
                ucsc.run(weatherData, parameters, outputs);
            }
            else if (modelUnderOptimization == "IPI")
            {
                ipi.run(weatherData, parameters, outputs);
            }
            else if (modelUnderOptimization == "Rule310")
            {
                rule310.run(weatherData, parameters, outputs);
            }
            else if (modelUnderOptimization == "Misfits")
            {
                misfits.run(weatherData, parameters, outputs);
            }
            else if (modelUnderOptimization == "Laore")
            {

                laore.run(weatherData, parameters, outputs);
            }




            #region detailed run
            double EPI_index = 0;
            double DMCast_PomSum = 0;
            double IPI_index_sum = 0;
            double UCSC_HT = 0;
            if (areEPIDMCASTexecutable)
            {
                if (modelUnderOptimization == "EPI")
                {
                    #region EPI                                               
                    EPI_ke.Add(outputs.outputsEPI.ke);
                    EPI_pe.Add(outputs.outputsEPI.pe);
                    //to cumulate EPI
                    EPI_index = outputs.outputsEPI.epi;
                    //reinitialize EPI_index sum each year on 1st october						
                    if (weatherData.Date.Month == 10 && weatherData.Date.Day == 1)
                    {
                        EPI_index = 0;
                    }
                    #endregion
                }

                #region DMCast                        
                if (modelUnderOptimization == "DMCast")
                {
                    DMCast_Pom.Add(dmcast.Pom(weatherData, parameters.dmcastParameters));
                    DMCast_Ra.Add(dmcast.RAi(weatherData));
                    //to cumulate Pom
                    DMCast_PomSum = outputs.outputsDMCast.pomsum;
                    //reinitialize DMCast each year on 1st October (as in the updated version of the model)
                    //See "Plant Health Progress, 2007, 8.1: 66"
                    if (weatherData.Date.Month == 09 && weatherData.Date.Day == 22) //change this to 1 Oct in updated model
                    {
                        DMCast_PomSum = 0;
                    }
                }
                #endregion
            }

            #region IPI             
            if (modelUnderOptimization == "IPI")
            {
                IPI_Ri.Add(ipi.Ri(weatherData, parameters.ipiParameters));
                IPI_Tmeani.Add(ipi.Tmeani(weatherData, parameters.ipiParameters));
                IPI_Lwi.Add(ipi.Lwi(weatherData, parameters.ipiParameters));
                IPI_Rhi.Add(ipi.Rhi(weatherData, parameters.ipiParameters));
                IPI_index.Add(ipi.IPI_index(weatherData, parameters.ipiParameters));
                //to cumulate IPI index
                IPI_index_sum = outputs.outputsIPI.ipisum;
                //reinitialize IPI each year
                if (weatherData.Date.DayOfYear == 1)
                {
                    IPI_index_sum = 0;
                }
            }
            #endregion

            #region UCSC Model   
            if (modelUnderOptimization == "UCSC")
            {
                UCSC_HTi.Add(outputs.outputsUCSC.hti);
                UCSC_DOR.Add(outputs.outputsUCSC.dor);
                UCSC_GER.Add(outputs.outputsUCSC.ger);
                //to cumulate hydrothermal time (HT)
                UCSC_HT = outputs.outputsUCSC.hts;
            }

            //reinitialize HT each year
            //if (weatherData.Date.Month == 1)
            //{
            //    UCSC_HT = 0;
            //}
            #endregion

            #endregion

            //phenology run (daily time step)
            if (weatherData.Date.Hour == 00)
            {
                //call phenology model
                InputDaily inputDaily = new InputDaily();
                inputDaily.Tmax = (float)Temperatures.Max();
                inputDaily.Tmin = (float)Temperatures.Min();
                inputDaily.Date = weatherData.Date;
                //call phenology models
                chilling.runDormancy(inputDaily, parameters, outputs);
                forcing.runForcing(inputDaily, parameters, outputs);

                if(outputs.outputsPhenology.bbchPhenophase <=10)
                {
                    #region reinitialize pressure
                    pressureRule310 = 0;
                    pressureEPI = 0;
                    pressureIPI = 0;
                    pressureDMCast = 0;
                    pressureMagarey = 0;
                    pressureUCSC = 0;
                    pressureMisfits = 0;
                    pressureLaore = 0;
                    #endregion
                }
            }

            //each day
            if (weatherData.Date.Hour == 00)
            {
                #region Populate daily model outputs
                modelsOutput = new OutputsDaily();
                modelsOutput.Input.Date = weatherData.Date;
                modelsOutput.Input.Site = weatherFile;
                modelsOutput.Input.Tmax = (float)Temperatures.Max();
                modelsOutput.Input.Tmin = (float)Temperatures.Min();
                modelsOutput.Tmean = Temperatures.Average();
                modelsOutput.Input.RHmax = (float)RelativeHumidity.Max();
                modelsOutput.Input.RHmin = (float)RelativeHumidity.Min();
                modelsOutput.RHmean = RelativeHumidity.Average();
                modelsOutput.Input.Precipitation = (float)Precipitation.Sum();
                modelsOutput.Input.LeafWetnessDuration = (float)Leafwetness.Sum();

                modelsOutput.chillRate = outputs.outputsPhenology.chillRate;
                modelsOutput.chillState = outputs.outputsPhenology.chillState;
                modelsOutput.antiChillRate = outputs.outputsPhenology.antiChillRate;
                modelsOutput.forcingRate = outputs.outputsPhenology.forcingRate;
                modelsOutput.forcingState = outputs.outputsPhenology.forcingState;
                modelsOutput.cycleCompletionPercentage = outputs.outputsPhenology.cycleCompletionPercentage;
                modelsOutput.bbchCode = outputs.outputsPhenology.bbchPhenophaseCode;
                modelsOutput.bbchPhase = outputs.outputsPhenology.bbchPhenophase;
                modelsOutput.plantSusceptibility = outputs.outputsPhenology.plantSusceptibility;

                #region binary outputs (Final outputs)
                
                // 310
                modelsOutput.infectionRule310 = outputs.outputsRule310.infectionEvents.Any(rule310Infection =>
                rule310Infection.infectionDate > weatherData.Date.AddHours(-24)) ? 1 : 0;
                pressureRule310 += modelsOutput.infectionRule310;
                modelsOutput.pressureRule310 += pressureRule310;
                // EPI
                modelsOutput.infectionEPI = outputs.outputsEPI.infectionEvents.Any(epiInfection => epiInfection.infectionDate > weatherData.Date.AddHours(-24)) ? 1 : 0;
                pressureEPI += modelsOutput.infectionEPI;
                modelsOutput.pressureEPI += pressureEPI;
                // IPI
                modelsOutput.infectionIPI = outputs.outputsIPI.infectionEvents.Any(ipiInfection => ipiInfection.infectionDate > weatherData.Date.AddHours(-24)) ? 1 : 0;
                pressureIPI += modelsOutput.infectionIPI;
                modelsOutput.pressureIPI += pressureIPI;
                // DMCast
                modelsOutput.infectionDMCast = outputs.outputsDMCast.infectionEvents.Any(dmcastInfection => dmcastInfection.infectionDate > weatherData.Date.AddHours(-24)) ? 1 : 0;
                pressureDMCast += modelsOutput.infectionDMCast;
                modelsOutput.pressureDMCast += pressureDMCast;
                // Magarey
                modelsOutput.infectionMagarey = outputs.outputsMagarey.infectionEvents.Any(magareyInfection => magareyInfection.infectionDate > weatherData.Date.AddHours(-24)) ? 1 : 0;
                pressureMagarey += modelsOutput.infectionMagarey;
                modelsOutput.pressureMagarey += pressureMagarey;
                // UCSC
                //modelsOutput.infectionUCSC = outputs.outputsUCSC.infectionEvents.Any(ucscInfection => ucscInfection.infectionDate > weatherData.Date.AddHours(-24)) ? 1 : 0;
                //pressureUCSC += modelsOutput.infectionUCSC;
                //modelsOutput.pressureUCSC += pressureUCSC;
                var uCSCInfectionsToRemove = outputs.outputsUCSC.infectionEvents
                .Where(rossiInfection => rossiInfection.infectionDate > weatherData.Date.AddHours(-24))
                .ToList();
                modelsOutput.infectionUCSC = uCSCInfectionsToRemove.Any() ? 1 : 0;

                //foreach (var rossiInfection in uCSCInfectionsToRemove)
                //{
                //    outputs.outputsUCSC.infectionEvents.Remove(rossiInfection);
                //}
                pressureUCSC += modelsOutput.infectionUCSC;
                modelsOutput.pressureUCSC += pressureUCSC;
                // Misfits
                modelsOutput.infectionMisfits = outputs.outputsMisfits.infectionEvents.Any(misfitsInfection => misfitsInfection.infectionDate > weatherData.Date.AddHours(-24)) ? 1 : 0;
                pressureMisfits += modelsOutput.infectionMisfits;
                modelsOutput.pressureMisfits += pressureMisfits;
                // Laore
                modelsOutput.infectionLaore = outputs.outputsLaore.infectionEvents.Any(laoreInfection => laoreInfection.infectionDate > weatherData.Date.AddHours(-24)) ? 1 : 0;
                pressureLaore += modelsOutput.infectionLaore;
                modelsOutput.pressureLaore += pressureLaore;
                #endregion

                #region binary Onset outputs  
                // Laore
                modelsOutput.onsetLaore = outputs.outputsLaore.infectionEvents.Any(laoreOnset =>
                laoreOnset.onsetDate > weatherData.Date.AddHours(-24)) ? 1 : 0;
                // Magarey
                modelsOutput.onsetMagarey = outputs.outputsMagarey.infectionEvents.Any(magareyOnset => 
                magareyOnset.onsetDate > weatherData.Date.AddHours(-24)) ? 1 : 0;
                // Misfits
                modelsOutput.onsetMisfits = outputs.outputsMisfits.infectionEvents.Any(misfitsOnset =>
                misfitsOnset.onsetDate > weatherData.Date.AddHours(-24)) ? 1 : 0;
                //Rule310
                modelsOutput.onsetRule310 = outputs.outputsRule310.infectionEvents.Any(rule310Onset =>
                rule310Onset.onsetDate > weatherData.Date.AddHours(-24)) ? 1 : 0;
                //DMCast
                modelsOutput.onsetDMCast = outputs.outputsDMCast.infectionEvents.Any(dmcastOnset =>
                dmcastOnset.onsetDate > weatherData.Date.AddHours(-24)) ? 1 : 0;
                //EPI
                modelsOutput.onsetEPI = outputs.outputsEPI.infectionEvents.Any(epiOnset =>
                epiOnset.onsetDate > weatherData.Date.AddHours(-24)) ? 1 : 0;
                //IPI
                modelsOutput.onsetIPI = outputs.outputsIPI.infectionEvents.Any(ipiOnset =>
                ipiOnset.onsetDate > weatherData.Date.AddHours(-24)) ? 1 : 0;
                //UCSC
                modelsOutput.onsetUCSC = outputs.outputsUCSC.infectionEvents.Any(ucscOnset =>
                ucscOnset.onsetDate > weatherData.Date.AddHours(-24)) ? 1 : 0;
                #endregion

                #region Intermediate model outputs
                if (modelUnderOptimization == "EPI")
                {
                    if (EPI_ke.Count > 0)
                    {
                        modelsOutput.EPI_ke = EPI_ke.Last();
                        modelsOutput.EPI_pe = EPI_pe.Max();
                    }

                    modelsOutput.EPI_index = EPI_index;
                }
                if (modelUnderOptimization == "DMCast")
                {
                    //DMCast
                    if (DMCast_Ra.Count > 0)
                    {
                        modelsOutput.DMCast_Ra = DMCast_Ra.Max();
                        modelsOutput.DMCast_Pom = DMCast_Pom.Max();
                    }
                    modelsOutput.DMCast_PomSum = DMCast_PomSum;
                }

                if (modelUnderOptimization == "IPI")
                {
                    //IPI
                    if (IPI_Tmeani.Count > 0)
                    {
                        modelsOutput.IPI_Tmeani = IPI_Tmeani.Max();
                        modelsOutput.IPI_Ri = IPI_Ri.Max();
                        modelsOutput.IPI_Lwi = IPI_Lwi.Max();
                        modelsOutput.IPI_Rhi = IPI_Rhi.Max();
                        modelsOutput.IPI_index = IPI_index.Max();

                    }

                    modelsOutput.IPI_index_sum = IPI_index_sum;
                }


                //UCSC
                if (modelUnderOptimization == "UCSC")
                {
                    if (UCSC_HTi.Count > 0)
                    {
                        modelsOutput.UCSC_HTi = UCSC_HTi.Max();
                        modelsOutput.UCSC_HT = UCSC_HT;
                        modelsOutput.UCSC_DOR = UCSC_DOR.Max();
                        modelsOutput.UCSC_GER = UCSC_GER.Max();
                    }
                }
                #endregion

                #endregion

                #region Reinitialize list of variables
                Temperatures = new List<double>();
                Precipitation = new List<double>();
                RelativeHumidity = new List<double>();
                Leafwetness = new List<double>();
                EPI_ke = new List<double>();
                EPI_pe = new List<double>();
                DMCast_Pom = new List<double>();
                DMCast_Ra = new List<double>();
                IPI_Tmeani = new List<double>();
                IPI_Ri = new List<double>();
                IPI_Rhi = new List<double>();
                IPI_Lwi = new List<double>();
                IPI_index = new List<double>();
                UCSC_DOR = new List<double>();
                UCSC_GER = new List<int>();
                UCSC_HTi = new List<double>();
                #endregion

            }
        }

        #region detailed bbch parameters
        private static Parameters generateDetailedPhenologyParameters(Parameters simpleParameters)
        {
            Parameters detailedParameters = (Parameters)simpleParameters;
            detailedParameters = new Parameters();
            detailedParameters.rule310Parameters = simpleParameters.rule310Parameters;
            detailedParameters.magareyParameters = simpleParameters.magareyParameters;
            detailedParameters.epiParameters = simpleParameters.epiParameters;
            detailedParameters.ipiParameters = simpleParameters.ipiParameters;
            detailedParameters.laoreParameters = simpleParameters.laoreParameters;
            detailedParameters.misfitsParameters = simpleParameters.misfitsParameters;
            detailedParameters.ucscParameters = simpleParameters.ucscParameters;
            detailedParameters.dmcastParameters = simpleParameters.dmcastParameters;
            detailedParameters.phenologyParameters = simpleParameters.phenologyParameters;

            //if bbch99 is not present, include it
            if (!simpleParameters.bbchParameters.ContainsKey(99))
            {
                simpleParameters.bbchParameters.Add(99, new parametersBBCH());
                simpleParameters.bbchParameters[99].cycleCompletion = 100;
            }

            List<int> keys = simpleParameters.bbchParameters.Keys.AsEnumerable().ToList();

            for (int i = 0; i < keys.Count; i++)
            {
                //get current key
                int currentBbch = keys[i];
                int nextBbch = 0;
                //NOTE: assumption, simulations only before bbch 90
                if (currentBbch < 99)
                {
                    nextBbch = keys[i + 1];

                    int bbchGap = nextBbch - currentBbch;

                    for (int availableBbch = 0; availableBbch < bbchGap; availableBbch++)
                    {
                        int thisBbch = currentBbch + availableBbch;
                        detailedParameters.bbchParameters.Add(thisBbch,
                            new parametersBBCH());

                        parametersBBCH phenologyParameters = new parametersBBCH();
                        //compute the growing degree days at flowering
                        float gddFlowering = simpleParameters.bbchParameters[65].cycleCompletion / 100 *
                      simpleParameters.phenologyParameters.CycleLength;

                        float floweringFraction = simpleParameters.bbchParameters[65].cycleCompletion / 100;
                        if (nextBbch < 65)
                        {
                            phenologyParameters.cycleCompletion =
                            linearInterpolator(currentBbch, simpleParameters.bbchParameters[currentBbch].cycleCompletion,
                            nextBbch, simpleParameters.bbchParameters[nextBbch].cycleCompletion, thisBbch) *
                            floweringFraction;

                        }
                        else if (nextBbch == 65)
                        {
                            phenologyParameters.cycleCompletion =
                            linearInterpolator(currentBbch, simpleParameters.bbchParameters[currentBbch].cycleCompletion,
                            nextBbch, 100, thisBbch) *
                            floweringFraction;
                        }
                        else if (nextBbch < 99)
                        {

                            phenologyParameters.cycleCompletion =
                                linearInterpolator(currentBbch, simpleParameters.bbchParameters[currentBbch].cycleCompletion,
                           nextBbch, floweringFraction * 100 +
                           simpleParameters.bbchParameters[nextBbch].cycleCompletion / 100 *
                           (100 - simpleParameters.bbchParameters[currentBbch].cycleCompletion),
                           thisBbch);

                        }
                        else
                        {
                            float start = (floweringFraction * 100) + simpleParameters.bbchParameters[currentBbch].cycleCompletion *
                           (1 - floweringFraction);

                            phenologyParameters.cycleCompletion =
                           linearInterpolator(currentBbch, start,
                           nextBbch, 100, thisBbch);
                        }

                    
                        //add the istance to the dictionary
                        detailedParameters.bbchParameters[thisBbch] = phenologyParameters;
                    }
                }

            }


            return detailedParameters;

        }

        public static float linearInterpolator(int currentBbch, float currentValue, int nextBbch, float nextValue, int thisBbch)
        {
            float interpolatedValue = 0;


            int x1 = currentBbch;
            float y1 = currentValue;
            int x2 = nextBbch;
            float y2 = nextValue;

            if ((x2 - x1) == 0)
            {
                interpolatedValue = (y2 + y1) / 2;
            }
            else
            {
                interpolatedValue = y1 + (thisBbch - x1) * (y2 - y1) / (x2 - x1);
            }


            return interpolatedValue;
        }
        #endregion

    }

    public static class Extend
    {
        public static double StandardDeviation(this IEnumerable<double> values)
        {
            double avg = values.Average();
            return Math.Sqrt(values.Average(v => Math.Pow(v - avg, 2)));
        }

        public static void AddRange(this Dictionary<DateTime, Input> dictionary, IEnumerable<KeyValuePair<DateTime, Input>> items)
        {
            foreach (var item in items)
            {
                dictionary.Add(item.Key, item.Value);
            }
        }
    }
}
