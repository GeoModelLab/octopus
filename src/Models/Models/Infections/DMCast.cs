using Models.Datatype;

namespace Models.Infections
{
    //DMCast model - Park EW, Seem RC, Gadoury DM, Pearson RC (1997) DMCast: A prediction model for grape downy mildew development. Vitic Enol Sci 52:182–139
    public class DMCast
    {
        #region local variables
        //Input lists
        List<Input> past_24Counts = new List<Input>();
        List<Input> PastMonth = new List<Input>();
        List<Input> ooGermCount = new List<Input>();

        //to compute climatic variables		
        public Dictionary<int, double> ClimaticRainfallSum = new Dictionary<int, double>();
        public Dictionary<int, double> ClimaticRainyDays = new Dictionary<int, double>();
        public Dictionary<int, double> ClimaticStdRainfallSum = new Dictionary<int, double>();
        #endregion

        #region model run
        //N.B. This section could be changed as new versions of the models implement only a simple threshold
        //Based on the temperatures and Precipitations considered for estimating oospore germination (see above)
        //plus a treshold on BBCH 15, see "Plant Health Progress, 2007, 8.1: 66"	
        public void run(Input Input, Parameters Parameters, Output Output)
        {
            
            //if there is germination
            if (oosporeGerm(Input, Parameters, Output.outputsDMCast) == 1)
            {
                //instance of an infection event
                var infectionEvent = new DMcastInfectionEvent();
                infectionEvent.germinationDate = Input.Date;
                Output.outputsDMCast.infectionEvents.Add(infectionEvent);
                infectionEvent.idInfection = Output.outputsDMCast.infectionEvents.Count();
            }

            //loop over infection events
            foreach (var singleEvent in Output.outputsDMCast.infectionEvents)
            {
                var infectionEvent = singleEvent as DMcastInfectionEvent;

                #region Sporangia germination
                infectionEvent.SporangiaGermHours += 1;
                if (infectionEvent.SporangiaGermHours >= Parameters.dmcastParameters.daysForSporangiaGerm * 24
                    && infectionEvent.SporangiaGermHours <= (Parameters.dmcastParameters.daysForSporangiaGerm + 6) * 24 &&
                    infectionEvent.SporangiaGermination == 0)
                {
                    infectionEvent.TemperatureSum += Input.Temperature;
                    if (Input.Date.Hour == 00)
                    {
                        if (infectionEvent.TemperatureSum >= Parameters.dmcastParameters.tempThresholdSporangiaGerm * 24)
                        {
                            infectionEvent.SporangiaGermination = 1;
                            infectionEvent.SporangiaGermDate = Input.Date;
                        }
                        infectionEvent.TemperatureSum = 0;
                    }
                }
                #endregion

                #region Infection occurrence
                //TODO: not used here
                double BBCH = 0;
                if (infectionEvent.SporangiaGermination == 1)
                {
                    infectionEvent.InfectionHours += 1;
                    if (infectionEvent.InfectionHours <= Parameters.dmcastParameters.daysForInfection * 24)
                    {
                        infectionEvent.TemperatureSum += Input.Temperature;
                        infectionEvent.RainSumSplash += Input.Precipitation;
                        if (Input.Date.Hour == 00)
                        {
                            if (infectionEvent.TemperatureSum >= Parameters.dmcastParameters.tempThresholdInf * 24 &&
                                infectionEvent.RainSumSplash > Parameters.dmcastParameters.precThresholdInf &&
                                BBCH >= 0)
                            {
                                infectionEvent.Infection = 1;
                                infectionEvent.infectionDate = Input.Date;
                            }
                            infectionEvent.TemperatureSum = 0;
                            infectionEvent.RainSumSplash = 0;
                        }
                    }
                }
                #endregion
            }
            ////remove uncompleted events
            DateTime lastDayOfTheYear = new DateTime(Input.Date.Year, 12, 31);

            Output.outputsDMCast.infectionEvents.RemoveAll(infectionEvent =>
            {
                var dmcastInfection = infectionEvent as DMcastInfectionEvent;

                // Check if it's the last day of the year and Infection is 0
                return Input.Date == lastDayOfTheYear && dmcastInfection.Infection == 0;
            });
        }
        #endregion

        #region intermediate functions

        //Daily precipitations
        private double Ri(Input Input)
        {
            double Ri = 0;
            DateTime currentDate = Input.Date;

            //define time step and count limits
            int Hour = Input.Date.Hour;
            //add one grid weather to the list
            past_24Counts.Add(Input);
            //define condition and compute 
            if (Hour == 00)
            {
                Ri = past_24Counts.Where(x => x.Precipitation > 0.2).
                                   Select(x => x.Precipitation).Sum();
                past_24Counts = new List<Input>();

            }
            //Compute ke
            return Ri;
        }

        //compute monthly Rain effect on oospore germination Ra
        //Define container for internal calculation
        List<double> RaList = new List<double>();
        List<int> RainyDays = new List<int>();
        public double RAi(Input Input)
        {
            double Ra;
            double _Ri = Ri(Input);
            int lastDayOfMonth = DateTime.DaysInMonth(Input.Date.Year, Input.Date.Month);
            DateTime currentDate = Input.Date;
            DateTime endDate = new DateTime(Input.Date.Year, 1, 31);
            DateTime startDate = new DateTime(Input.Date.Year, 09, 21); //change to 30 sept in updated model
                                                                        //add weather variables to the list
            PastMonth.Add(Input);
            if (currentDate <= endDate || currentDate >= startDate)
            {
                //number of monthly rainy days
                int RDm = RainyDays.Sum();

                if (currentDate.Hour == 00)
                {
                    //define minimum threshold for daily rainfall Hm
                    double Hm = ClimaticRainfallSum[currentDate.Month] / ClimaticRainyDays[currentDate.Month];
                    //define maximum threshold for daily rainfall HM
                    double HM = (ClimaticRainfallSum[currentDate.Month] + ClimaticStdRainfallSum[currentDate.Month]) / ClimaticRainyDays[currentDate.Month]; //to verify this condition see TRAN MANH SUNG et. al, 1990 (Plant Disease).

                    double POS = 0; //positive effect of Rain over oospore maturation
                    double LAC = 0; //lack of rain - negative effect of rain on oospore maturation
                    double EXC = 0; //excess of rain - negative effect of rain on oospore maturation
                                    //Try considering only rainy days
                    if (_Ri > 0.2)
                    {
                        if (_Ri <= HM && _Ri > Hm)
                        {
                            POS = _Ri;
                        }
                        else if (_Ri < Hm)
                        {
                            LAC = Hm - _Ri;
                        }
                        else if (_Ri > HM)
                        {
                            EXC = _Ri - HM;
                            POS = HM;
                        }
                    }
                    //negative effect of rain on oospore maturation
                    double NEG = Math.Abs(EXC - LAC);
                    //final calculation of the maturation index
                    double Rai = POS - NEG;
                    RaList.Add(Rai);

                    //restart after 31st January 
                    if (currentDate.Date == startDate)
                    {
                        RaList = new List<double>();
                    }
                }
            }
            //Compute cumulative effect of rainfall (Ra)
            Ra = RaList.Sum();
            return Ra;
        }

        //Compute probability of oospore maturation (Pom)
        List<double> PomList = new List<double>();
        public double Pom(Input Input, parametersDMCast dmcastPar)
        {
            double Pom = 0;
            DateTime startDate = new DateTime(Input.Date.Year, 1, 31);
            DateTime endDate = new DateTime(Input.Date.Year, 09, 22); //change to 1 Oct in updated model
            if (Input.Date > startDate && Input.Date < endDate)
            {
                if (Input.Date.Hour == 00)
                {
                    double Ra = RAi(Input);
                    //Compute probability of oospore maturation Pom
                    double mu = dmcastPar.muK1 - 0.3 * Ra;
                    double sigma = dmcastPar.sigmaK1 + 0.02 * Ra;
                    Pom = 1 / (sigma * Math.Sqrt(2 * Math.PI)) *
                        Math.Exp(-Math.Pow(Input.Date.DayOfYear - mu, 2) / (2 * Math.Pow(sigma, 2)));
                    PomList.Add(Pom);

                    // restart each year					
                    if (Input.Date == endDate.AddDays(-1))
                    {
                        PomList = new List<double>();
                    }
                }
            }
            return Pom;
        }

        //Calculate oospore germination days
        public int oosporeGerm(Input Input, Parameters Parameters, OutputsDMCast dmcastOutput)
        {
            int ooGerm = 0;
            ooGermCount.Add(Input);
            double PomSum = PomList.Sum();
            //cumulated Pom to output
            dmcastOutput.pomsum = PomSum;
            DateTime endDate = new DateTime(Input.Date.Year, 09, 22); //change to 1 Oct in updated model

            if (PomSum >= Parameters.dmcastParameters.thresholdPOM && Input.Date < endDate)
            {
                if (Input.Date.Hour == 00)
                {
                    double DailyPrec = ooGermCount.Where(x => x.Precipitation > 0.2).
                                      Select(x => x.Precipitation).Sum();

                    double DailyTemp = ooGermCount.Select(x => x.Temperature).Average();

                    if (DailyTemp > Parameters.dmcastParameters.tempThresholdOosporeGerm && DailyPrec > Parameters.dmcastParameters.precThresholdOosporeGerm)
                    {
                        ooGerm = 1;
                    }

                    ooGermCount = new List<Input>();
                }
            }
            return ooGerm;
        }

        #endregion
    }
}
