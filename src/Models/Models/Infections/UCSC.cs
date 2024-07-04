using Models.Datatype;

namespace Models.Infections
{
    //UCSC model - Rossi V, Caffi T, Giosuè S, Bugiani R (2008). A mechanistic model simulating primary infections of downy mildew in grapevine. Ecol Modell 212, 480-491. https://doi.org/10.1016/j.ecolmodel.2007.10.046
    public class UCSC
    {
        #region local variable
        List<double> HT_List = new List<double>();
        #endregion

        #region model run
        public void run(Input Input, Parameters Parameters, Output Outputs)
        {
            List<double> gerdor = GER(Input, Outputs.outputsUCSC, Parameters.ucscParameters);
            int ger = (int)gerdor[1]; //cast double to int
            double dor = gerdor[0];
            //send GER to output
            Outputs.outputsUCSC.ger = ger;

            if (ger == 1)
            {
                var infectionEvent = new UCSCInfectionEvent();
                infectionEvent.startGermination = Input.Date;
                infectionEvent.idInfection = Outputs.outputsUCSC.infectionEvents.Count();

                //compute density of each oospore cohorts
                double dor_1 = 0;
                if (Outputs.outputsUCSC.infectionEvents.Count > 0)
                {
                    dor_1 = infectionEvent.cohortDensity;
                }
                else
                {
                    dor_1 = 0.03;
                }
                Outputs.outputsUCSC.infectionEvents.Add(infectionEvent);
                infectionEvent.cohortDensity = dor - dor_1;
            }

            double _HTi = HTi(Input, Outputs.outputsUCSC, Parameters.ucscParameters);

            foreach (var singleEvent in Outputs.outputsUCSC.infectionEvents)
            {
                var infectionEvent = singleEvent as UCSCInfectionEvent;

                #region Germination process 
                //calculate germination process for each event 
                if (infectionEvent.germination == 0 && _HTi > 0)
                {
                    infectionEvent.HydroThermalTime += _HTi;
                }
                //When the cumulated HydroThermal time reaches 1, the germination process is considered complete 
                if (infectionEvent.HydroThermalTime > Parameters.ucscParameters.germinationThreshold)
                {
                    infectionEvent.germination = 1;
                    infectionEvent.endGermination = Input.Date;
                    if (infectionEvent.germination == 1)
                    {
                        infectionEvent.HydroThermalTime = Parameters.ucscParameters.germinationThreshold;
                    }
                }

                #endregion

                #region Sporangia survival
                if (infectionEvent.germination == 1)
                {
                    //compute survival of sporangia
                    double SUS = 1 / (24 * (5.67 - 0.47 * (Input.Temperature * (1 - Input.RelativeHumidity / 100)) +
                        0.01 * Math.Pow(Input.Temperature * (1 - Input.RelativeHumidity / 100), 2)));
                    infectionEvent.sporangiaSurvival += SUS;
                    //condition of survival
                    if (infectionEvent.sporangiaSurvival <= Parameters.ucscParameters.sporangiaSurvivalThreshold)
                    {
                        infectionEvent.germinatedOospores = infectionEvent.cohortDensity;
                    }
                    else if (infectionEvent.sporangiaSurvival > Parameters.ucscParameters.sporangiaSurvivalThreshold)
                    {
                        infectionEvent.germinatedOospores = 0;
                    }
                }
                #endregion

                #region Zoospore release
                if (infectionEvent.germinatedOospores > 0)
                {
                    if (Input.Precipitation > 0.2)
                    {
                        infectionEvent.wetHoursRelease += 1;
                        infectionEvent.temperatureSumRel += Input.Temperature;
                        infectionEvent.averageTempWetHoursRel = infectionEvent.temperatureSumRel / infectionEvent.wetHoursRelease;
                    }
                    else
                    {
                        infectionEvent.wetHoursRelease += 0;
                    }
                    //Zoospore release
                    double releaseThreshold = Math.Exp((-1.022 + 19.634) /
                        infectionEvent.averageTempWetHoursRel);
                    if (infectionEvent.wetHoursRelease >= releaseThreshold)
                    {
                        infectionEvent.zoosporeRelease = 1;
                        infectionEvent.zoosporeReleaseDate = Input.Date;
                        infectionEvent.releasedZoospores = infectionEvent.germinatedOospores;
                    }
                    else
                    {
                        infectionEvent.zoosporeRelease = 0;
                    }
                }
                #endregion

                #region Zoospore survival
                if (infectionEvent.zoosporeRelease == 1)
                {
                    infectionEvent.hoursAfterRelease += 1;
                    if (Input.Precipitation > 0.2)
                    {
                        infectionEvent.wetHoursSurvival += 1;
                    }
                    else
                    {
                        infectionEvent.wetHoursSurvival += 0;
                    }
                    //define survival threshold
                    int survivalTime = infectionEvent.hoursAfterRelease / infectionEvent.wetHoursSurvival;
                    if (survivalTime > Parameters.ucscParameters.zoosporeSurvivalThreshold)
                    {
                        infectionEvent.releasedZoospores = 0;
                    }
                }
                #endregion

                #region Zoospore dispersal
                if (infectionEvent.releasedZoospores > 0)
                {
                    if (Input.Precipitation > 0.2)
                    {
                        infectionEvent.dispersedZoospores = infectionEvent.releasedZoospores;
                        infectionEvent.zoosporeDispersal = 1;
                    }
                }
                #endregion

                #region Infection 
                if (infectionEvent.zoosporeDispersal == 1)
                {
                    if (Input.Precipitation > 0.2 && infectionEvent.infection == 0)
                    {
                        infectionEvent.wetHoursInfection += 1;
                        infectionEvent.temperatureSumInf += Input.Temperature;
                        infectionEvent.averageTempWetHoursInf = infectionEvent.temperatureSumInf / infectionEvent.wetHoursInfection;
                    }
                    else
                    {
                        infectionEvent.wetHoursInfection += 0;
                    }
                    //Infection condition
                    double WDTWD = infectionEvent.wetHoursInfection * infectionEvent.averageTempWetHoursInf;
                    if (WDTWD >= Parameters.ucscParameters.infectionThreshold)
                    {
                        infectionEvent.infection = 1;
                        infectionEvent.infectionDate = Input.Date;
                        infectionEvent.zoosporesCausingInfection = infectionEvent.dispersedZoospores;
                    }
                    else
                    {
                        infectionEvent.infection = 0;
                        infectionEvent.zoosporesCausingInfection = 0;
                    }
                }
                #endregion

                #region Oil Spot Appearance
                //computes hourly progress of incubation (INC) and its lower and upper confidence intervals
                if (infectionEvent.infection == 1)
                {
                    double INClow = 1 / (24 * (45.1 - 3.45 * Input.Temperature + 0.073 * Math.Pow(Input.Temperature, 2)));
                    double INCUp = 1 / (24 * (59.9 - 4.55 * Input.Temperature + 0.095 * Math.Pow(Input.Temperature, 2)));
                    if (INClow > 0 || INCUp > 0)
                    {
                        infectionEvent.infectionCILow += INClow;
                        infectionEvent.infectionCIUp += INCUp;
                        if (infectionEvent.infectionCILow >= Parameters.ucscParameters.incubationLowerThreshold &&
                            infectionEvent.infectionCIUp <= Parameters.ucscParameters.incubationUpperThreshold)
                        {
                            infectionEvent.incubationPeriod = 1;
                            infectionEvent.incubationDate = Input.Date;
                        }
                        else
                        {
                            infectionEvent.incubationPeriod = 0;
                        }
                    }
                }
                #endregion
            }

            DateTime lastDayOfTheYear = new DateTime(Input.Date.Year, 12, 31);

            Outputs.outputsUCSC.infectionEvents.RemoveAll(infectionEvent =>
            {
                var ucscInfection = infectionEvent as UCSCInfectionEvent;

                // Check if it's the last day of the year and infection is 0
                return Input.Date == lastDayOfTheYear && ucscInfection.infection == 0;
            });
        }
        #endregion

        #region intermediate functions
        //calculate hydro-thermal time (HT)
        public double HTi(Input Input, OutputsUCSC uCSCOutputs, parametersUCSC rossiParameters)
        {
            double HTi = 0;
            DateTime lastDayOfTheYear = new DateTime(Input.Date.Year, 12, 31);
            //VPD
            if (Input.Date < lastDayOfTheYear)
            {
                //VPD calculation method by Monteith and Unsworth (1990)
                double SVP = 610.7 * Math.Pow(10, 7.5 * Input.Temperature / (237.3 + Input.Temperature));
                double VPD = SVP * (1 - Input.RelativeHumidity / 100);
                //define parameter M
                double M = 0;
                if (Input.Precipitation > 0 || VPD <= rossiParameters.vpdThreshold)
                {
                    M = 1;
                }
                else if (Input.Precipitation == 0 && VPD > rossiParameters.vpdThreshold)
                {
                    M = 0;
                }
                //compute HTi
                if (Input.Temperature > 0)
                {
                    HTi = M / (1330.1 - 116.19 * Input.Temperature + 2.6256 * Math.Pow(Input.Temperature, 2));
                }
                HT_List.Add(HTi);
            }
            else
            {
                HT_List = new List<double>();
            }
            //send hydrothermal time to outputs
            uCSCOutputs.hti = HTi;

            return HTi;
        }
        //calculate rate of dormancy braking DOR
        public double DOR(Input Input, OutputsUCSC uCSCOutputs)
        {
            double DOR;
            double HT = HT_List.Sum();
            //send cumulated hydrothermal time to outputs
            uCSCOutputs.hts = HT;

            DOR = Math.Exp(-15.891 * Math.Exp(-0.653 * (HT + 1)));

            //send DOR to outputs
            uCSCOutputs.dor = DOR;
            return DOR;
        }
        //calculate germination of oospores (GER)
        List<double> CumRain = new List<double>();
        public List<double> GER(Input Input, OutputsUCSC uCSCOutputs, parametersUCSC rossiParameters)
        {
            List<double> DOR_GER = new List<double>();
            DOR_GER.Add(DOR(Input, uCSCOutputs));

            if (DOR_GER[0] >= rossiParameters.dormancyBrakingLowerThreshold && DOR_GER[0] <=
                rossiParameters.dormancyBrakingUpperThreshold)
            {
                if (Input.Precipitation > 0.2)
                {
                    CumRain.Add(Input.Precipitation);
                    double RainEvent = CumRain.Sum();
                    if (RainEvent > Input.Precipitation)
                    {
                        DOR_GER.Add(0);
                    }
                    else
                    {
                        DOR_GER.Add(1);
                    }
                }
                else
                {
                    CumRain = new List<double>();
                }
            }
            if (DOR_GER.Count == 1)
            {
                DOR_GER.Add(0);
            }
            return DOR_GER;
        }
        #endregion

    }
}
