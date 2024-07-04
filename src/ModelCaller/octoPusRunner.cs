using System.Reflection;
using Models.Datatype;
using Models.Infections;
using Models.Phenology;
using System.Text;
using octoPusAI.Readers;

namespace octoPusAI.ModelCallers
{
    //this class calls the octoPus models, the RandomForest model and the LLama model
    internal class octoPusRunner
    {
      
        #region instance of RandomForest model (the brain)
        RInterface Rinterface = new RInterface();
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

        #region local variables 
        // instance of the weather reader class
        WeatherReader weatherReader = new WeatherReader();
        public string weatherFile;
        //dictionary to store the parameters
        public Dictionary<string, float> octoPusParameters = new Dictionary<string, float>();
        //dictionary to store the susceptibility parameters
        public Dictionary<int, parametersSusceptibility> BBCH_Susceptibility = new Dictionary<int, parametersSusceptibility>();
        public Dictionary<DateTime, OutputsDaily> date_outputs = new Dictionary<DateTime, OutputsDaily>();
        //these variables are set from the json configuration file
        public int startYear;
        public int endYear; 
        public float assistantRisk;
        public int veryHighModelsThreshold;
        public string Rversion;
        public string modelPath;
        public bool areEPIDMCASTexecutable;
        #endregion

        #region private variables 
        //list of hourly variables to compute daily data
        List<double> Temperatures = new List<double>();
        List<double> Precipitation = new List<double>();
        List<double> RelativeHumidity = new List<double>();
        List<double> Leafwetness = new List<double>();
        //specific variables for the detailed run
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
        public void octoPus(out Dictionary<DateTime, OutputsDaily> date_outputs)
        {
            //instance of LLama interface (the voice)
            LLamaInterface LLamaInterface = new LLamaInterface(modelPath);

             //reinitialize the date_outputs object
             date_outputs = new Dictionary<DateTime, OutputsDaily>();

            #region assign parameters to octoPus models
            
            #region assign infection model parameters

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
            #endregion

            //assign calibrated parameters
            foreach (var param in octoPusParameters.Keys)
            {
                //split class from param name
                string[] paramClass = param.Split('_');

                if (paramClass[0] == "Rule310")
                {
                    foreach (PropertyInfo prp in propsRule310)
                    {
                        if (paramClass[1] == prp.Name)
                        {
                            prp.SetValue(parRule310, (float)(octoPusParameters[param])); //set the values for this parameter
                        }
                    }
                }
                if (paramClass[0] == "Magarey")
                {
                    foreach (PropertyInfo prp in propsMagarey)
                    {
                        if (paramClass[1] == prp.Name)
                        {
                            prp.SetValue(parMagarey, (float)(octoPusParameters[param])); //set the values for this parameter
                        }

                    }
                }
                if (paramClass[0] == "EPI")
                {
                    foreach (PropertyInfo prp in propsEPI)
                    {
                        if (paramClass[1] == prp.Name)
                        {
                            prp.SetValue(parEPI, (float)(octoPusParameters[param])); //set the values for this parameter
                        }

                    }
                }
                if (paramClass[0] == "IPI")
                {
                    foreach (PropertyInfo prp in propsIPI)
                    {
                        if (paramClass[1] == prp.Name)
                        {
                            prp.SetValue(parIPI, (float)(octoPusParameters[param])); //set the values for this parameter
                        }

                    }
                }
                if (paramClass[0] == "Laore")
                {
                    foreach (PropertyInfo prp in propsLaore)
                    {
                        if (paramClass[1] == prp.Name)
                        {
                            prp.SetValue(parLaore, (float)(octoPusParameters[param])); //set the values for this parameter
                        }

                    }
                }
                if (paramClass[0] == "Misfits")
                {
                    foreach (PropertyInfo prp in propsMisfits)
                    {
                        if (paramClass[1] == prp.Name)
                        {
                            prp.SetValue(parMisfits, (float)(octoPusParameters[param])); //set the values for this parameter
                        }

                    }
                }
                if (paramClass[0] == "UCSC")
                {
                    foreach (PropertyInfo prp in propsUCSC)
                    {
                        if (paramClass[1] == prp.Name)
                        {
                            prp.SetValue(parUCSC, (float)(octoPusParameters[param])); //set the values for this parameter
                        }

                    }
                }
                if (paramClass[0] == "DMCast")
                {
                    foreach (PropertyInfo prp in propsDMCast)
                    {
                        if (paramClass[1] == prp.Name)
                        {
                            prp.SetValue(parDMCast, (float)(octoPusParameters[param])); //set the values for this parameter
                        }

                    }
                }
                if (paramClass[0] == "Phenology")
                {
                    foreach (PropertyInfo prp in propsPhenology)
                    {
                        if (paramClass[1] == prp.Name)
                        {
                            prp.SetValue(parPhenology, (float)(octoPusParameters[param])); //set the values for this parameter
                        }

                    }
                }
                if (paramClass[0] == "BBCH")
                {
                    foreach (PropertyInfo prp in propsBBCH)
                    {
                        parametersBBCH = new parametersBBCH();
                        parameters.bbchParameters.Add(int.Parse(paramClass[1].Substring(4, 2)), parametersBBCH);
                        if (parameters.bbchParameters[int.Parse(paramClass[1].Substring(4, 2))].
                            cycleCompletion == 0)
                        {
                            parameters.bbchParameters[int.Parse(paramClass[1].Substring(4, 2))].cycleCompletion =
                                (float)(octoPusParameters[param]); //set the values for this parameter
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
            #endregion

            #region assign phenology parameters for detailed crop parameters estimation
            parameters.bbchParameters = parameters.bbchParameters.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value); ;
            Parameters _detailedCropParameters = generateDetailedPhenologyParameters(parameters);
            parameters = _detailedCropParameters;
            parameters.bbchSusceptibilityParameters = BBCH_Susceptibility;
            #endregion
            
            #endregion

            //read weather data
            Dictionary<DateTime, Input> weatherData = weatherReader.readHourly(weatherFile, startYear, endYear);

            if (!areEPIDMCASTexecutable)
            {
                //the infection models that require climatic averages
                epi = new EPI();
                dmcast = new DMCast();
                //read the weather data for the climatic averages for EPI and DMCAST
                historicalRun(weatherData);
            }

            //initialize variables for each site
            var outputs = new Output();
            //reinitialize the date_outputs object
            date_outputs = new Dictionary<DateTime, OutputsDaily>();
            //initialize the daily outputs object
            OutputsDaily outputsDaily = new OutputsDaily();

            //assign the file of the LLM
            LLamaInterface.modelPath = modelPath;

            //loop over dates
            foreach (var hour in weatherData.Keys)
            {
                //call the octoPus model
                modelCall(weatherData[hour], parameters, outputs, out outputsDaily, LLamaInterface);

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

            //write the outputs from the octoPus models
            writeOctoPusOutputs(weatherFile, date_outputs);
        }

        //this method computes the climatic monthly averages for the EPI and DMCAST models
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
                ClimaticMonthlyRainfall[Input.Date.Month].Add(Input.Precipitation);
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

            if (areEPIDMCASTexecutable)
            {
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
            }
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
            "bbchCode,bbchPhase,plantSusceptibility," +
            // Main model outputs
            "Rule310,Epi,Ipi,Dmcast,Magarey,UCSC,misfits,laore," +
            // Main model outputs (pressure)
            "pressureRule310,pressureEpi,pressureIpi,pressureDmcast,pressureMagarey,pressureUCSC,pressureMisfits,pressureLaore";
            
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

                    #region phenology model outputs
                    line.Append($"{date_outputs[date].chillState},");
                    line.Append($"{date_outputs[date].chillRate},");
                    line.Append($"{date_outputs[date].antiChillRate},");
                    line.Append($"{date_outputs[date].forcingRate},");
                    line.Append($"{date_outputs[date].forcingState},");
                    line.Append($"{date_outputs[date].cycleCompletionPercentage},");
                    line.Append($"{date_outputs[date].bbchCode},");
                    line.Append($"{date_outputs[date].bbchPhase},");
                    #endregion

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

                    #region Model suboutputs (detailed run, now commented)
                    // EPI
                    //line.Append($"{date_outputs[date].EPI_ke},");
                    //line.Append($"{date_outputs[date].EPI_pe},");
                    //line.Append($"{date_outputs[date].EPI_index},");
                    //// DMCast      date_outputs[date]
                    //line.Append($"{date_outputs[date].DMCast_Ra},");
                    //line.Append($"{date_outputs[date].DMCast_Pom},");
                    //line.Append($"{date_outputs[date].DMCast_PomSum},");
                    //// IPI         date_outputs[date]
                    //line.Append($"{date_outputs[date].IPI_Tmeani},");
                    //line.Append($"{date_outputs[date].IPI_Ri},");
                    //line.Append($"{date_outputs[date].IPI_Rhi},");
                    //line.Append($"{date_outputs[date].IPI_Lwi},");
                    //line.Append($"{date_outputs[date].IPI_index},");
                    //line.Append($"{date_outputs[date].IPI_index_sum},");
                    //// UCSC       date_outputs[date]
                    //line.Append($"{date_outputs[date].UCSC_HTi},");
                    //line.Append($"{date_outputs[date].UCSC_HT},");
                    //line.Append($"{date_outputs[date].UCSC_DOR},");
                    //line.Append($"{date_outputs[date].UCSC_GER}");
                    #endregion

                    toWrite.Add(line.ToString());

                }
            }

            // Find the last occurrence of the directory separator character
            int lastIndex = site.LastIndexOf('\\');
            string siteShort = site.Substring(lastIndex + 1);
            
            //save the file
            System.IO.File.WriteAllLines(@"outputs//diseaseModels//" + siteShort, toWrite);
            #endregion


        }
        #endregion

        // Execute a single hourly timestep
        public void modelCall(Input inputs, Parameters parameters, Output outputs, out OutputsDaily modelsOutput, LLamaInterface LLamaInterface)
        {
            //initialize the daily output object
            modelsOutput = new OutputsDaily();

            //add weather data to the lists
            Temperatures.Add(inputs.Temperature);
            Precipitation.Add(inputs.Precipitation);
            RelativeHumidity.Add(inputs.RelativeHumidity);
            Leafwetness.Add(inputs.LeafWetness);

            //reinizialize objects at the start of the year
            if (inputs.Date.DayOfYear == 1)
            {
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

            //reinitialize outputsPhenology each year at the start of the season DOY 300 (25th October)
            if (inputs.Date.DayOfYear == 300 && inputs.Date.Hour == 0)
            {
                outputs.outputsPhenology = new OutputsPhenology();
                //reinitialize the UCSC model at the start of the season
                ucsc = new UCSC();
            }


            //call the octoPus infection models (tentacles)            
            magarey.run(inputs, parameters, outputs);
            if (areEPIDMCASTexecutable)
            {
                epi.run(inputs, parameters, outputs);
                dmcast.run(inputs, parameters, outputs);
            }
            ipi.run(inputs, parameters, outputs);
            ucsc.run(inputs, parameters, outputs);         
            rule310.run(inputs, parameters, outputs);
            misfits.run(inputs, parameters, outputs);
            laore.run(inputs, parameters, outputs);


            #region detailed run
            double EPI_index = 0;
            double DMCast_PomSum = 0;
            double IPI_index_sum = 0;
            double UCSC_HT = 0;

            if (areEPIDMCASTexecutable)
            {
                #region EPI                                               
                EPI_ke.Add(outputs.outputsEPI.ke);
                EPI_pe.Add(outputs.outputsEPI.pe);
                //to cumulate EPI
                EPI_index = outputs.outputsEPI.epi;
                //reinitialize EPI_index sum each year on 1st October						
                if (inputs.Date.Month == 10 && inputs.Date.Day == 1)
                {
                    EPI_index = 0;
                }
                #endregion

                #region DMCast                        
                DMCast_Pom.Add(dmcast.Pom(inputs, parameters.dmcastParameters));
                DMCast_Ra.Add(dmcast.RAi(inputs));
                //to cumulate Pom
                DMCast_PomSum = outputs.outputsDMCast.pomsum;
                //reinitialize DMCast each year on 1st October (as in the updated version of the model)
                //See "Plant Health Progress, 2007, 8.1: 66"
                if (inputs.Date.Month == 09 && inputs.Date.Day == 22) //change this to 1 Oct in updated model
                {
                    DMCast_PomSum = 0;
                }
                #endregion
            }

            #region IPI                        
            IPI_Ri.Add(ipi.Ri(inputs, parameters.ipiParameters));
            IPI_Tmeani.Add(ipi.Tmeani(inputs, parameters.ipiParameters));
            IPI_Lwi.Add(ipi.Lwi(inputs, parameters.ipiParameters));
            IPI_Rhi.Add(ipi.Rhi(inputs, parameters.ipiParameters));
            IPI_index.Add(ipi.IPI_index(inputs, parameters.ipiParameters));
            //to cumulate IPI index
            IPI_index_sum = outputs.outputsIPI.ipisum;
            //reinitialize IPI each year
            if (inputs.Date.DayOfYear == 1)
            {
                IPI_index_sum = 0;
            }
            #endregion

            #region UCSC Model                        
            UCSC_HTi.Add(outputs.outputsUCSC.hti);
            UCSC_DOR.Add(outputs.outputsUCSC.dor);
            UCSC_GER.Add(outputs.outputsUCSC.ger);
            //to cumulate hydrothermal time (HT)
            UCSC_HT = outputs.outputsUCSC.hts;

            //reinitialize HT each year
            if (inputs.Date.Month == 1)
            {
                UCSC_HT = 0; ;
            }
            #endregion

            #endregion

            //phenology run (daily time step)
            if (inputs.Date.Hour == 00)
            {
                //call phenology model
                InputDaily inputDaily = new InputDaily();
                inputDaily.Tmax = (float)Temperatures.Max();
                inputDaily.Tmin = (float)Temperatures.Min();
                inputDaily.Date = inputs.Date;
                //call phenology models
                chilling.runDormancy(inputDaily, parameters, outputs);
                forcing.runForcing(inputDaily, parameters, outputs);
            }

            //each day
            if (inputs.Date.Hour == 00)
            {
                #region Populate daily model outputs
                //initialize the daily output object
                modelsOutput = new OutputsDaily();
                //populate the properties of the daily output object with synthetic input weather data
                modelsOutput.Input.Date = inputs.Date;
                modelsOutput.Input.Site = weatherFile;
                modelsOutput.Input.Tmax = (float)Temperatures.Max();
                modelsOutput.Input.Tmin = (float)Temperatures.Min();
                modelsOutput.Tmean = Temperatures.Average();
                modelsOutput.Input.RHmax = (float)RelativeHumidity.Max();
                modelsOutput.Input.RHmin = (float)RelativeHumidity.Min();
                modelsOutput.RHmean = RelativeHumidity.Average();
                modelsOutput.Input.Precipitation = (float)Precipitation.Sum();
                modelsOutput.Input.LeafWetnessDuration = (float)Leafwetness.Sum();
                //populate the outputs of the phenological model
                modelsOutput.chillRate = outputs.outputsPhenology.chillRate;
                modelsOutput.chillState = outputs.outputsPhenology.chillState;
                modelsOutput.antiChillRate = outputs.outputsPhenology.antiChillRate;
                modelsOutput.forcingRate = outputs.outputsPhenology.forcingRate;
                modelsOutput.forcingState = outputs.outputsPhenology.forcingState;
                modelsOutput.cycleCompletionPercentage = outputs.outputsPhenology.cycleCompletionPercentage;
                modelsOutput.bbchCode = outputs.outputsPhenology.bbchPhenophaseCode;
                modelsOutput.bbchPhase = outputs.outputsPhenology.bbchPhenophase;
                //populate the output of the susceptibility model
                modelsOutput.plantSusceptibility = outputs.outputsPhenology.plantSusceptibility;

                #region binary outputs outputs (Final outputs)
                //Rule 310
                modelsOutput.infectionRule310 = outputs.outputsRule310.infectionEvents.
                    Any(rule310Infection =>
                rule310Infection.infectionDate > inputs.Date.AddHours(-24)) ? 1 : 0;
                pressureRule310 += modelsOutput.infectionRule310;
                modelsOutput.pressureRule310 += pressureRule310;
                // EPI
                modelsOutput.infectionEPI = outputs.outputsEPI.infectionEvents.
                    Any(epiInfection => epiInfection.infectionDate > inputs.Date.AddHours(-24)) ? 1 : 0;
                pressureEPI += modelsOutput.infectionEPI;
                modelsOutput.pressureEPI += pressureEPI;
                // IPI
                modelsOutput.infectionIPI = outputs.outputsIPI.infectionEvents.
                    Any(ipiInfection => ipiInfection.infectionDate > inputs.Date.AddHours(-24)) ? 1 : 0;
                pressureIPI += modelsOutput.infectionIPI;
                modelsOutput.pressureIPI += pressureIPI;
                // DMCast
                modelsOutput.infectionDMCast = outputs.outputsDMCast.infectionEvents.
                    Any(dmcastInfection => dmcastInfection.infectionDate > inputs.Date.AddHours(-24)) ? 1 : 0;
                pressureDMCast += modelsOutput.infectionDMCast;
                modelsOutput.pressureDMCast += pressureDMCast;
                // Magarey
                modelsOutput.infectionMagarey = outputs.outputsMagarey.infectionEvents.
                    Any(magareyInfection => magareyInfection.infectionDate > inputs.Date.AddHours(-24)) ? 1 : 0;
                pressureMagarey += modelsOutput.infectionMagarey;
                modelsOutput.pressureMagarey += pressureMagarey;
                // UCSC
                var uCSCInfectionsToRemove = outputs.outputsUCSC.infectionEvents
                    .Where(uCSCInfection => uCSCInfection.infectionDate > inputs.Date.AddHours(-24))
                    .ToList();
                modelsOutput.infectionUCSC = uCSCInfectionsToRemove.Any() ? 1 : 0;
                foreach (var uCSCInfection in uCSCInfectionsToRemove)
                {
                    outputs.outputsUCSC.infectionEvents.Remove(uCSCInfection);
                }
                pressureUCSC += modelsOutput.infectionUCSC;
                modelsOutput.pressureUCSC += pressureUCSC;
                // Misfits
                modelsOutput.infectionMisfits = outputs.outputsMisfits.infectionEvents.
                    Any(misfitsInfection => misfitsInfection.infectionDate > inputs.Date.AddHours(-24)) ? 1 : 0;
                pressureMisfits += modelsOutput.infectionMisfits;
                modelsOutput.pressureMisfits += pressureMisfits;
                // Laore
                modelsOutput.infectionLaore = outputs.outputsLaore.infectionEvents.
                    Any(laoreInfection => laoreInfection.infectionDate > inputs.Date.AddHours(-24)) ? 1 : 0;
                pressureLaore += modelsOutput.infectionLaore;
                modelsOutput.pressureLaore += pressureLaore;
                #endregion

                #region Intermediate model outputs
                if (EPI_ke.Count > 0)
                {
                    modelsOutput.EPI_ke = EPI_ke.Last();
                    modelsOutput.EPI_pe = EPI_pe.Last();
                }

                modelsOutput.EPI_index = EPI_index;
                //DMCast
                if (DMCast_Ra.Count > 0)
                {
                    modelsOutput.DMCast_Ra = DMCast_Ra.Max();
                    modelsOutput.DMCast_Pom = DMCast_Pom.Max();
                }

                modelsOutput.DMCast_PomSum = DMCast_PomSum;
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
                //UCSC
                if (UCSC_HTi.Count > 0)
                {
                    modelsOutput.UCSC_HTi = UCSC_HTi.Max();
                    modelsOutput.UCSC_HT = UCSC_HT;
                    modelsOutput.UCSC_DOR = UCSC_DOR.Max();
                    modelsOutput.UCSC_GER = UCSC_GER.Max();
                }
                #endregion

                #endregion

                #region populate data structure for machine learning
                //create an instance of the machine learning model outputs
                ModelOutputsML ModelOutputsML = new ModelOutputsML();
                //assign the outputs of the single infection models to the instance of the machine learning model outputs
                ModelOutputsML.model_date_infection.Add("Rule310", new Dictionary<DateTime, int>());
                ModelOutputsML.model_date_infection["Rule310"].Add(inputs.Date, modelsOutput.infectionRule310);
                ModelOutputsML.model_date_infection.Add("Epi", new Dictionary<DateTime, int>());
                ModelOutputsML.model_date_infection["Epi"].Add(inputs.Date, modelsOutput.infectionEPI);
                ModelOutputsML.model_date_infection.Add("Ipi", new Dictionary<DateTime, int>());
                ModelOutputsML.model_date_infection["Ipi"].Add(inputs.Date, modelsOutput.infectionIPI);
                ModelOutputsML.model_date_infection.Add("Dmcast", new Dictionary<DateTime, int>());
                ModelOutputsML.model_date_infection["Dmcast"].Add(inputs.Date, modelsOutput.infectionDMCast);
                ModelOutputsML.model_date_infection.Add("Magarey", new Dictionary<DateTime, int>());
                ModelOutputsML.model_date_infection["Magarey"].Add(inputs.Date, modelsOutput.infectionMagarey);
                ModelOutputsML.model_date_infection.Add("UCSC", new Dictionary<DateTime, int>());
                ModelOutputsML.model_date_infection["UCSC"].Add(inputs.Date, modelsOutput.infectionUCSC);
                ModelOutputsML.model_date_infection.Add("misfits", new Dictionary<DateTime, int>());
                ModelOutputsML.model_date_infection["misfits"].Add(inputs.Date, modelsOutput.infectionMisfits);
                ModelOutputsML.model_date_infection.Add("laore", new Dictionary<DateTime, int>());
                ModelOutputsML.model_date_infection["laore"].Add(inputs.Date, modelsOutput.infectionLaore);
                ModelOutputsML.model_date_pressure.Add("Rule310", new Dictionary<DateTime, int>());
                ModelOutputsML.model_date_pressure["Rule310"].Add(inputs.Date, modelsOutput.pressureRule310);
                ModelOutputsML.model_date_pressure.Add("Epi", new Dictionary<DateTime, int>());
                ModelOutputsML.model_date_pressure["Epi"].Add(inputs.Date, modelsOutput.pressureEPI);
                ModelOutputsML.model_date_pressure.Add("Ipi", new Dictionary<DateTime, int>());
                ModelOutputsML.model_date_pressure["Ipi"].Add(inputs.Date, modelsOutput.pressureIPI);
                ModelOutputsML.model_date_pressure.Add("Dmcast", new Dictionary<DateTime, int>());
                ModelOutputsML.model_date_pressure["Dmcast"].Add(inputs.Date, modelsOutput.pressureDMCast);
                ModelOutputsML.model_date_pressure.Add("Magarey", new Dictionary<DateTime, int>());
                ModelOutputsML.model_date_pressure["Magarey"].Add(inputs.Date, modelsOutput.pressureMagarey);
                ModelOutputsML.model_date_pressure.Add("UCSC", new Dictionary<DateTime, int>());
                ModelOutputsML.model_date_pressure["UCSC"].Add(inputs.Date, modelsOutput.pressureUCSC);
                ModelOutputsML.model_date_pressure.Add("misfits", new Dictionary<DateTime, int>());
                ModelOutputsML.model_date_pressure["misfits"].Add(inputs.Date, modelsOutput.pressureMisfits);
                ModelOutputsML.model_date_pressure.Add("laore", new Dictionary<DateTime, int>());
                ModelOutputsML.model_date_pressure["laore"].Add(inputs.Date, modelsOutput.pressureLaore);
                //assign BBCH code and plant susceptibility to the RandomForest model
                ModelOutputsML.BBCH = modelsOutput.bbchCode;
                ModelOutputsML.susceptibility = modelsOutput.plantSusceptibility;
                Rinterface.modelOutputsML = ModelOutputsML;
                //call the machine learning model
                ModelOutputsML = Rinterface.MLmodelCall(Rversion);

                //the Llama assistant is called only if BBCH code is > 11 and the date is before July 31 (212) 
                if (modelsOutput.bbchCode > 10 && inputs.Date.DayOfYear < 212)
                {
                    //call the Llama assistant
                    LLamaInterface.CallAsyncAndWaitOnResult(weatherFile, inputs.Date, 
                        ModelOutputsML, assistantRisk, veryHighModelsThreshold);
                }
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
        //this static method generates the detailed phenology parameters by linear interpolation of the BBCH parameters in the octoPusParameters.csv file
        private static Parameters generateDetailedPhenologyParameters(Parameters simpleParameters)
        {
            //instance of the detailed parameters
            Parameters detailedParameters = (Parameters)simpleParameters;
            detailedParameters = new Parameters();
            //the parameters of the infection models are passed to the detailed parameters
            detailedParameters.rule310Parameters = simpleParameters.rule310Parameters;
            detailedParameters.magareyParameters = simpleParameters.magareyParameters;
            detailedParameters.epiParameters = simpleParameters.epiParameters;
            detailedParameters.ipiParameters = simpleParameters.ipiParameters;
            detailedParameters.laoreParameters = simpleParameters.laoreParameters;
            detailedParameters.misfitsParameters = simpleParameters.misfitsParameters;
            detailedParameters.ucscParameters = simpleParameters.ucscParameters;
            detailedParameters.dmcastParameters = simpleParameters.dmcastParameters;
            detailedParameters.phenologyParameters = simpleParameters.phenologyParameters;

            //if bbch99 is not present, include it as the last key
            if (!simpleParameters.bbchParameters.ContainsKey(99))
            {
                simpleParameters.bbchParameters.Add(99, new parametersBBCH());
                simpleParameters.bbchParameters[99].cycleCompletion = 100;
            }
            //get the keys of the simple parameters
            List<int> keys = simpleParameters.bbchParameters.Keys.AsEnumerable().ToList();

            //loop over the keys of the simple parameters
            for (int i = 0; i < keys.Count; i++)
            {
                //get current key
                int currentBbch = keys[i];
                int nextBbch = 0;
                //NOTE: assumption, simulations only before bbch 99
                if (currentBbch < 99)
                {
                    //get the next key
                    nextBbch = keys[i + 1];
                    //compute the BBCH gap between the two keys
                    int bbchGap = nextBbch - currentBbch;
                    //loop over the BBCH gap
                    for (int availableBbch = 0; availableBbch < bbchGap; availableBbch++)
                    {
                        //compute the current BBCH code
                        int thisBbch = currentBbch + availableBbch;
                        //add the BBCH code to the detailed parameters
                        detailedParameters.bbchParameters.Add(thisBbch, new parametersBBCH());
                        //initialize the parametersBBCH object
                        parametersBBCH phenologyParameters = new parametersBBCH();
                        
                        float floweringFraction = simpleParameters.bbchParameters[65].cycleCompletion / 100;
                        //these are the BBCH codes before flowering
                        if (nextBbch < 65)
                        {
                            phenologyParameters.cycleCompletion =
                            linearInterpolator(currentBbch, simpleParameters.bbchParameters[currentBbch].cycleCompletion,
                            nextBbch, simpleParameters.bbchParameters[nextBbch].cycleCompletion, thisBbch) *
                            floweringFraction;

                        }
                        //this is the BBCH code for flowering
                        else if (nextBbch == 65)
                        {
                            phenologyParameters.cycleCompletion =
                            linearInterpolator(currentBbch, simpleParameters.bbchParameters[currentBbch].cycleCompletion,
                            nextBbch, 100, thisBbch) *
                            floweringFraction;
                        }
                        //these are the BBCH codes after flowering
                        else if (nextBbch < 99)
                        {
                            phenologyParameters.cycleCompletion =
                                linearInterpolator(currentBbch, simpleParameters.bbchParameters[currentBbch].cycleCompletion,
                           nextBbch, floweringFraction * 100 +
                           simpleParameters.bbchParameters[nextBbch].cycleCompletion / 100 *
                           (100 - simpleParameters.bbchParameters[currentBbch].cycleCompletion),
                           thisBbch);
                        }
                        else //this is the BBCH code for senescence
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
            //return the detailed parameters
            return detailedParameters;
        }

        //this static method computes the linear interpolation between two values
        public static float linearInterpolator(int currentBbch, float currentValue, int nextBbch, float nextValue, int thisBbch)
        {
            //initialize the interpolated value
            float interpolatedValue = 0;
            //the linear interpolation formula
            int x1 = currentBbch;
            float y1 = currentValue;
            int x2 = nextBbch;
            float y2 = nextValue;

            //if the difference between the two BBCH codes is 0, the interpolated value is the average of the two values
            if ((x2 - x1) == 0)
            {
                interpolatedValue = (y2 + y1) / 2;
            }
            else //otherwise, the interpolated value is computed using the linear interpolation formula
            {
                interpolatedValue = y1 + (thisBbch - x1) * (y2 - y1) / (x2 - x1);
            }
            //return the interpolated value
            return interpolatedValue;
        }
        #endregion
    }

    //extend the IEnumerable class to compute the standard deviation
    public static class Extend
    {
        public static double StandardDeviation(this IEnumerable<double> values)
        {
            double avg = values.Average();
            return Math.Sqrt(values.Average(v => Math.Pow(v - avg, 2)));
        }
    }
}
