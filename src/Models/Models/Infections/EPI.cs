using Models.Datatype;

namespace Models.Infections
{
    //EPI model - Strizyk S (1983). Modèle d’état potentiel d’infection: application a Plasmopara viticola. Assoc. Coord. Tech. Agric. 1:46.
    public class EPI
    {
        #region local variables
        List<Input> DecadeCounts = new List<Input>();
        List<Input> MonthlyCounts = new List<Input>();
        List<Input> KeCounts = new List<Input>();
        List<Input> InfectionCount = new List<Input>();

        public Dictionary<int, double> ClimaticAverageTemperature = new Dictionary<int, double>();
        public Dictionary<int, double> ClimaticRainfallSum = new Dictionary<int, double>();
        public Dictionary<int, double> ClimaticRainyDays = new Dictionary<int, double>();
        public Dictionary<int, double> ClimaticRelativeHumidityNight = new Dictionary<int, double>();
        #endregion

        #region model run
        public void run(Input Input, Parameters Parameters, Output Output)
        {
           
            double PE = Pe(Input, Parameters.epiParameters, Output.outputsEPI);
            double KE = Ke(Input, Parameters.epiParameters, Output.outputsEPI);

            //Add Pe
            pesum.Add(PE);
            //Add Ke
            kesum.Add(KE);

            //Sum Pe
            double PES = pesum.Sum();

            //sum Ke
            double KES = kesum.Sum();

            //empty Lists at 1 October
            if (Input.Date.Month == 10)
            {
                pesum = new List<double>();
                kesum = new List<double>();
            }

            InfectionCount.Add(Input);

            if (Input.Date.Hour == 00)
            {
                Output.outputsEPI.epi = KES + PES;
                if (Output.outputsEPI.epi > Parameters.epiParameters.alertThreshold && Input.Date.Month < 10)
                {
                    double DailyPrec = InfectionCount.Where(x => x.Precipitation > 0.2).
                                       Select(x => x.Precipitation).Sum();

                    double DailyTemp = InfectionCount.Select(x => x.Temperature).Average();

                    //TODO: not used here
                    double BBCHPhase = 0;

                    if (DailyTemp > Parameters.epiParameters.tempThresholdInf &&
                        DailyPrec > Parameters.epiParameters.precThresholdInf &&
                        BBCHPhase >= 0)
                    {
                        var infectionEvent = new GenericInfection();
                        infectionEvent.infectionDate = Input.Date;
                        Output.outputsEPI.infectionEvents.Add(infectionEvent);
                    }
                    InfectionCount = new List<Input>();
                }
            }
        }

        #endregion

        #region intermediate functions
        //Kinetic energy
        public double Ke(Input Input, parametersEPI epiParameters, OutputsEPI outputsEPI)
        {
            double Ke = 0;

            //define limits
            if (Input.Date.Month >= 4 && Input.Date.Month <= 9)
            {

                //define time step and count limits
                int Hour = Input.Date.Hour;
                DateTime currentDate = Input.Date;

                //add one grid weather to the list
                KeCounts.Add(Input);

                //define weather variables
                double RelativeHumidityDay = 0;
                double TemperatureAverage = 0;

                //define condition and compute 
                if (Hour == 00 && KeCounts.Count >= 23)
                {
                    RelativeHumidityDay = KeCounts.Where(x => x.Date.Hour >= 9 && x.Date.Hour <= 18).
                        Select(x => x.RelativeHumidity).Average();
                    TemperatureAverage = KeCounts.Select(x => x.Temperature).Average();
                    //Square root does not handle negative values
                    if (TemperatureAverage < 0)
                    {
                        TemperatureAverage = 0;
                    }
                    //define UR range calculation
                    double Um = (5 * ClimaticRelativeHumidityNight[Input.Date.Month] +
                                                3 * RelativeHumidityDay) / 8;
                    if (Um <= epiParameters.urLowerThreshold && Um >= epiParameters.urUpperThreshold)
                    {
                        Um = 0;
                    }
                    //compute cinetic energy
                    Ke = epiParameters.keConstant * ((Math.Pow(Um, 2) *
                    Math.Sqrt(TemperatureAverage) - Math.Pow(RelativeHumidityDay, 2) *
                    Math.Sqrt(ClimaticAverageTemperature[Input.Date.Month])) / 100);

                    //empty ThisDayAverages and start over again
                    KeCounts = new List<Input>();
                }
            }
            //Output ke
            outputsEPI.ke = Ke;

            return Ke;

        }

        //monthly constant
        private double Ct(int Month)
        {
            double Ct;

            //no need to compute it internally, it is the argument of the method           

            switch (Month)
            {
                case 10:
                    Ct = 1.2;
                    break;
                case 11:
                    Ct = 1.2;
                    break;
                case 12:
                    Ct = 1;
                    break;
                case 1:
                    Ct = 0.8;
                    break;
                case 2:
                    Ct = 0.8;
                    break;
                case 3:
                    Ct = 0.8;
                    break;
                default:
                    Ct = 0;
                    break;
            }
            //separate algorithms: here only Ct 
            //Compute ke
            return Ct;
        }

        //Potential energy        
        //Use the same rule as the one of the new version of DMCast
        ////define list to cumulate potential energy
        List<double> pesum = new List<double>();
        //define list to cumulate ke
        List<double> kesum = new List<double>();
        public double Pe(Input Input, parametersEPI epiParameters, OutputsEPI outputsEPI)
        {
            //output variable
            double PE = 0;

            //first check if Pe needs to be computed
            DateTime currentDate = Input.Date;

            //Pe is calculated between 1 October and 31 March 
            if (currentDate.Month >= 9 || currentDate.Month <= 3)
            {
                //add one grid weather (hour) to the list
                DecadeCounts.Add(Input);
                MonthlyCounts.Add(Input);

                //no need to compute a list of ct, just a single value depending on the month
                double _Ct = Ct(currentDate.Month);

                //montly precipitations and temperatures are calculated over a 30 days moving window
                double Tm = 0;
                double Rm = 0;
                if (MonthlyCounts.Count == 30 * 24)
                {
                    MonthlyCounts.RemoveAt(0);

                    List<double> Precipitations = new List<double>();
                    List<double> Temperatures = new List<double>();

                    foreach (var gw in MonthlyCounts)
                    {
                        Precipitations.Add(gw.Precipitation);
                        Temperatures.Add(gw.Temperature);
                    }

                    //define monthly precipitation
                    Rm = Precipitations.Sum();
                    //define monthly average temperature
                    Tm = Temperatures.Average();

                    //Square root does not handle negative values
                    if (Tm < 0)
                    {
                        Tm = 0;
                    }

                }

                if (currentDate.Month >= 10 || currentDate.Month <= 3)
                {
                    //every 10 days
                    if (DecadeCounts.Count == 240)
                    {
                        //rainfall sum in the decade
                        double Rd = DecadeCounts.Where(x => x.Precipitation > 0.2).
                            Select(x => x.Precipitation).Sum();
                        //number of rainy days
                        //RDd = RainyDays.Sum();
                        int RainyDays = 0;
                        for (int i = 0; i < 240; i++)
                        {
                            if (DecadeCounts[i].Precipitation > 0.2)
                            {
                                RainyDays++;
                                int Hour = 24 - DecadeCounts[i].Date.Hour;
                                i = i + Hour;
                            }
                        }
                        double RDd = RainyDays;

                        //define negative energy (EN)
                        double EN = 0;
                        //define k
                        double k = Rd / RDd;
                        //EN calculation based on Tran Manh Sung, 1987
                        if (k >= 135 / ClimaticRainyDays[Input.Date.Month])
                        {
                            double a = ClimaticRainyDays[Input.Date.Month] * 1.5 / 18;
                            EN = a * Math.Log(k);
                        }

                        //Compute potential energy (pe)
                        double pe = 2 * _Ct * (Math.Sqrt(Rm) - Math.Sqrt(ClimaticRainfallSum[Input.Date.Month] * 0.95));

                        //compute positive energy (EP)
                        double EP = 0; ;
                        if (ClimaticAverageTemperature[Input.Date.Month] >= 0)
                        {
                            EP = epiParameters.peConstant * _Ct * (Math.Sqrt(Rm) * Math.Sqrt(Tm) - Math.Sqrt(ClimaticRainfallSum[Input.Date.Month] * 0.95) *
                                                Math.Sqrt(ClimaticAverageTemperature[Input.Date.Month] * 0.95));
                        }
                        else
                        {
                            EP = epiParameters.peConstant * _Ct * (Math.Sqrt(Rm) * Math.Sqrt(Tm) - Math.Sqrt(ClimaticRainfallSum[Input.Date.Month] * 0.95) * 0);
                        }

                        //Compute final Pe
                        PE = pe + EP - EN;

                        //empty List and start over again
                        DecadeCounts = new List<Input>();
                    }
                }
            }
            //Output ke
            outputsEPI.pe = PE;
            return PE;
        }

        #endregion

    }
}