using Models.Datatype;

namespace Models.Infections
{
    //IPI model - Gherardi I (2001). Modello a prognosi negativa per le infezioni primarie di Plasmopara viticola. Inf Agrar 57:83-86.
    public class IPI
    {
        #region local variables
        List<Input> Ri_counts = new List<Input>();
        List<Input> Tmini_counts = new List<Input>();
        List<Input> Tmeani_counts = new List<Input>();
        List<Input> Lwi_counts = new List<Input>();
        List<Input> Rhi_counts = new List<Input>();
        List<Input> IPI_counts = new List<Input>();
        List<Input> InfectionCount = new List<Input>();
        #endregion

        #region model run
        public void run(Input Input, Parameters Parameters, Output Output)
        {
            double IPI = IPI_index(Input, Parameters.ipiParameters);
            InfectionCount.Add(Input);
            if (Input.Date.Hour == 00)
            {
                IPISum.Add(IPI);
                double sumIPI = IPISum.Sum();
                //Send cumulated IPI to output
                Output.outputsIPI.ipisum = sumIPI;
                //Restart sum of IPI each year
                var lastDayOfTheYear = new DateTime(Input.Date.Year, 12, 31);
                if (Input.Date == lastDayOfTheYear)
                {
                    IPISum = new List<double>();
                }

                //verify infection condition
                if (sumIPI >= Parameters.ipiParameters.alertThreshold)
                {
                    double DailyPrec = InfectionCount.Where(x => x.Precipitation > 0.2).
                                       Select(x => x.Precipitation).Sum();

                    double DailyTemp = InfectionCount.Select(x => x.Temperature).Average();

                    //TODO: not used here
                    double BBCHPhase = 0;

                    if (DailyTemp > Parameters.ipiParameters.tempThresholdInf &&
                        DailyPrec > Parameters.ipiParameters.precThresholdInf &&
                        BBCHPhase >= Parameters.ipiParameters.bbchThreshold)
                    {
                        var infectionEvent = new GenericInfection();
                        infectionEvent.infectionDate = Input.Date;
                        Output.outputsIPI.infectionEvents.Add(infectionEvent);
                    }

                    InfectionCount = new List<Input>();
                }
            }
        }
        #endregion

        #region intermediate functions
        //compute rainfall index (Ri)
        public double Ri(Input Input, parametersIPI ipiPar)
        {
            double Ri = 0;
            //add one hour
            Ri_counts.Add(Input);
            //Runnning total of precipitations over 48h
            if (Ri_counts.Count == 48)
            {
                Ri_counts.RemoveAt(0);
            }

            List<double> Precipitations = new List<double>();

            foreach (var gw in Ri_counts)
            {
                Precipitations.Add(gw.Precipitation);
            }

            double Precipitation = Precipitations.Sum();

            //compute Ri
            if (Input.Date.Hour == 00)
            {
                Ri = 0.00667 + 0.194405 * Precipitation + 0.0002239 * Math.Pow(Precipitation, 2);
            }
            if (Ri < 0)
            {
                Ri = 0;
            }
            if (Ri > ipiPar.riUpperThreshold)
            {
                Ri = ipiPar.riUpperThreshold;
            }
            return Ri;
        }

        //compute temperature index based on min temperatures (Tmini)
        //this is not used for the computation of the IPI index but was icluded in the original spreadsheet that Riccardo Bugiani kindly 
        //supplied to us
        public double Tmini(Input Input)
        {
            double Tmini = 0;
            Tmini_counts.Add(Input);

            if (Input.Date.Hour == 00)
            {
                double Tmin = Tmini_counts.Select(x => x.Temperature).Min();
                Tmini = 0.047272 - 0.082915 * Tmin + 0.010239 * Math.Pow(Tmin, 2);

                if (Tmini < 0)
                {
                    Tmini = 0;
                }
                if (Tmini > 1)
                {
                    Tmini = 1;
                }
                Tmini_counts = new List<Input>();
            }
            return Tmini;
        }
        //compute temperature index based on mean temperature (Tmeani)
        public double Tmeani(Input Input, parametersIPI ipiPar)
        {
            double Tmeani = 0;
            Tmeani_counts.Add(Input);
            if (Input.Date.Hour == 00)
            {
                double Tmin = Tmeani_counts.Select(x => x.Temperature).Min();
                double Tmin_corr = 0;
                if (Tmin > ipiPar.tmeaniTminThreshold)
                {
                    Tmin_corr = 1;
                }
                else
                {
                    Tmin_corr = 0.35 + 0.05 * Tmin;
                }
                double Tmean = Tmeani_counts.Select(x => x.Temperature).Average();
                Tmeani = (-2.19247 + 0.259906 * Tmean - 0.000139 * Math.Pow(Tmean, 3) - 6.095832 * Math.Pow(10, -6) * Math.Pow(Tmean, 4)) * Tmin_corr;

                if (Tmeani < 0)
                {
                    Tmeani = 0;
                }
                if (Tmeani > ipiPar.tmeaniUpperThreshold)
                {
                    Tmeani = 1;
                }
                Tmeani_counts = new List<Input>();
            }
            return Tmeani;
        }
        //compute leaf wetness index (Lwi)
        public double Lwi(Input Input, parametersIPI ipiPar)
        {
            double Lwi = 0;
            Lwi_counts.Add(Input);
            if (Input.Date.Hour == 00)
            {
                double Lw = Lwi_counts.Select(x => x.LeafWetness).Sum();
                double DailyRain = Lwi_counts.Select(x => x.Precipitation).Sum();

                Lwi = 0.004 * Math.Pow(Lw, 2) + 0.008 * Lw - 0.01;
                if (Lwi > ipiPar.lwiLowerThreshold)
                {
                    Lwi = 1;
                }
                if (DailyRain <= 0.2 && Lw <= ipiPar.lwiUpperThreshold)
                {
                    Lwi = 0;
                }
                Lwi_counts = new List<Input>();
            }
            return Lwi;
        }
        //compute relative humidity index (Rhi)
        public double Rhi(Input Input, parametersIPI ipiPar)
        {
            double Rhi = 0;
            Rhi_counts.Add(Input);

            if (Input.Date.Hour == 00)
            {
                double URmean = Rhi_counts.Select(x => x.RelativeHumidity).Average();
                Rhi = (-69.994545 + 1.502 * URmean - 0.007818 * Math.Pow(URmean, 2)) / 2;

                if (Rhi < 0)
                {
                    Rhi = 0;
                }
                if (Rhi > ipiPar.rhiUpperThreshold)
                {
                    Rhi = 1;
                }
                Rhi_counts = new List<Input>();
            }
            return Rhi;
        }
        //EPI_index
        public double IPI_index(Input Input, parametersIPI ipiPar)
        {
            double IPI_index = 0;
            IPI_counts.Add(Input);
            if (Input.Date.Hour == 00)
            {
                //intermediate steps
                double DailyRain = IPI_counts.Select(x => x.Precipitation).Sum();
                double IPI_Lw_Ri = Ri(Input, ipiPar) + Lwi(Input, ipiPar);
                double IPI_LwRi_Tmean = 0;

                if (DailyRain == 0)
                {
                    IPI_LwRi_Tmean = 0;
                }
                else
                {
                    IPI_LwRi_Tmean = IPI_Lw_Ri * Tmeani(Input, ipiPar);
                }
                double IPI_Tmean_Ri = 0;
                if (DailyRain == 0)
                {
                    IPI_Tmean_Ri = 0;
                }
                else
                {
                    IPI_Tmean_Ri = Tmeani(Input, ipiPar) * Ri(Input, ipiPar);
                }
                //compute IPI_index
                double Tmin = IPI_counts.Select(x => x.Temperature).Min();
                double IPI_Tmean_Rhi = Tmeani(Input, ipiPar) * Rhi(Input, ipiPar);
                if (Tmin > ipiPar.ipiTminThreshold)
                {
                    if (IPI_LwRi_Tmean > IPI_Tmean_Rhi)
                    {
                        IPI_index = IPI_LwRi_Tmean;
                    }
                    else
                    {
                        IPI_index = IPI_Tmean_Rhi;
                    }
                }
                IPI_counts = new List<Input>();
            }
            return IPI_index;
        }

        //Use the same rule as the one of the new version of DMCast
        List<double> IPISum = new List<double>();
        #endregion
    }
}
