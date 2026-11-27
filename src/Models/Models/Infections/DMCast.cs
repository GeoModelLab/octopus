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
        //monthly accumulators for the rain effect index (Tran Manh Sung et al., 1990)
        private double _posM = 0, _excM = 0, _lacM = 0;
        private int _raLastMonth = -1;
        #endregion

        #region model run
        //N.B. This section could be changed as new versions of the models implement only a simple threshold
        //Based on the temperatures and Precipitations considered for estimating oospore germination (see above)
        //plus a treshold on BBCH 15, see "Plant Health Progress, 2007, 8.1: 66"

        public void run(Input Input, Parameters Parameters, Output Output)
        {
            #region Infection compute
            //if there is germination
            if (oosporeGerm(Input, Parameters, Output.outputsDMCast) == 1)
            {
                //instance of an infection event
                var infectionEvent = new DMcastInfectionEvent();
                infectionEvent.germinationDate = Input.Date;
                //track phenophase
                infectionEvent.phenophase = Output.outputsPhenology.bbchPhenophase;
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
                
                double BBCH = Output.outputsPhenology.bbchPhenophaseCode;
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
                                BBCH >= Parameters.dmcastParameters.bbchThreshold)
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
            #endregion

            #region Incubation

            //calculate incubation
            foreach (var infEvent in Output.outputsDMCast.infectionEvents)
            {
                // Cast to DMcastInfectionEvent to access specific properties
                var PosInfEvent = infEvent as DMcastInfectionEvent; //var for infection occurred

                if (PosInfEvent == null) continue;

                if (PosInfEvent.Infection == 1 && PosInfEvent.phenophase >= 10 && PosInfEvent.onsetDate.Year == 1)
                {
                    Utils.utilities.incubationEstimate(PosInfEvent, Parameters.incubationParameters, Input);
                }
            }
            #endregion

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
                    //restart at the beginning of the maturation season
                    if (currentDate.Date == startDate.Date)
                    {
                        RaList = new List<double>();
                        _posM = 0; _excM = 0; _lacM = 0; _raLastMonth = -1;
                    }

                    //define minimum threshold for daily rainfall Hm
                    double Hm = ClimaticRainfallSum[currentDate.Month] / ClimaticRainyDays[currentDate.Month];
                    //define maximum threshold for daily rainfall HM
                    double HM = (ClimaticRainfallSum[currentDate.Month] + ClimaticStdRainfallSum[currentDate.Month]) / ClimaticRainyDays[currentDate.Month]; //to verify this condition see TRAN MANH SUNG et. al, 1990 (Plant Disease).

                    double POS = 0; //positive effect of Rain over oospore maturation
                    double LAC = 0; //lack of rain - negative effect of rain on oospore maturation
                    double EXC = 0; //excess of rain - negative effect of rain on oospore maturation
                    if (_Ri > 0.2)
                    {
                        if (_Ri <= HM && _Ri > Hm) { POS = _Ri; }
                        else if (_Ri < Hm) { LAC = Hm - _Ri; }
                        else if (_Ri > HM) { EXC = _Ri - HM; POS = HM; }
                    }

                    //Tran Manh Sung et al. (1990), Plant Disease 74:120-124:
                    //POS(M) = SUM POS(d); NEG(M) = |SUM EXC(d) - SUM LAC(d)|; Im(M) = [POS(M) - NEG(M)] + Im(M-1).
                    //The negative effect is computed on monthly sums, so that excess and lack of rain compensate.
                    if (_raLastMonth != -1 && Input.Date.Month != _raLastMonth)
                    {
                        RaList.Add(_posM - Math.Abs(_excM - _lacM)); //index of the month just closed
                        _posM = 0; _excM = 0; _lacM = 0;
                    }
                    _raLastMonth = Input.Date.Month;

                    _posM += POS;
                    _excM += EXC;
                    _lacM += LAC;
                }
            }
            //cumulative effect of rainfall (Ra), including the month still open
            Ra = RaList.Sum() + (_posM - Math.Abs(_excM - _lacM));
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

            //the hourly window is closed every day, also before the POM threshold is reached
            if (Input.Date.Hour == 00)
            {
                double DailyPrec = ooGermCount.Where(x => x.Precipitation > 0.2).
                                  Select(x => x.Precipitation).Sum();

                double DailyTemp = ooGermCount.Select(x => x.Temperature).Average();

                if (PomSum >= Parameters.dmcastParameters.thresholdPOM && Input.Date < endDate)
                {
                    if (DailyTemp > Parameters.dmcastParameters.tempThresholdOosporeGerm &&
                        DailyPrec > Parameters.dmcastParameters.precThresholdOosporeGerm)
                    {
                        ooGerm = 1;
                    }
                }

                ooGermCount = new List<Input>();
            }
            return ooGerm;
        }

        #endregion
    }
}
