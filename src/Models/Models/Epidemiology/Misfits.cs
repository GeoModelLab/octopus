using Models.Datatype;

namespace Models.Infections
{
    //Misfits model - Misfits group (2022). A public decision support system for the assessment of plant disease infection risk shared by Italian regions. J Environ Manage 1:317:115365 https://www.sciencedirect.com/science/article/pii/S0301479722009380?via%3Dihub
    public class Misfits
    {
        #region local variables
        double _macrosporangia_formed_daily = 0;
        double _macrosporangia_spread_daily = 0;
        double _downyMildew_infection_daily = 0;
        double _counter_macrosporangia_suitability = 0;
        #endregion

        #region model run
        public void run(Input Input, Parameters Parameters, Output Output)
        {
            
            //Reinitialize the variables in the first hour of the day
            ReinitializeVariablesAtHour0(Input.Date.Hour, Output.outputsMisfits);

            //Check weather suitability for macrosporangia
            int macrosporangiaSuitability =
                MacrosporangiaFormationSuitability(Input, Parameters.misfitsParameters, Output.outputsMisfits);

            //Manage the increase of macrosporangia suitability
            if (macrosporangiaSuitability == 1)
            {
                Output.outputsMisfits.macrosporangia_suitability_hourly++;
            }
            else
            {
                Output.outputsMisfits.macrosporangia_suitability_hourly = 0;
            }
            Output.outputsMisfits.macrosporangia_suitability_daily += Output.outputsMisfits.macrosporangia_suitability_hourly;
            _counter_macrosporangia_suitability += macrosporangiaSuitability;


            //Compute temperature function for macrosporangia
            Output.outputsMisfits.macrosporangia_temperature_function = temperature_function(
                Input.Temperature, Parameters.misfitsParameters.TmaxMacrosporangiaFormation,
                Parameters.misfitsParameters.TminMacrosporangiaFormation,
                Parameters.misfitsParameters.ToptMacrosporangiaFormation);

            //Scale temperature function on moisture duration
            Output.outputsMisfits.macrosporangia_temp_moisture_function =
                MacrosporangiaMoistureFunction(Output.outputsMisfits, Parameters.misfitsParameters);

            //Check macrosporangia formation
            if (Output.outputsMisfits.macrosporangia_temp_moisture_function == 1)
            {
                Output.outputsMisfits.macrosporangia_formed_hourly = 1;
                Output.outputsMisfits.are_macrosporangia_formed = 1;
            }
            else
            {
                Output.outputsMisfits.macrosporangia_formed_hourly = 0;
            }
            //increase the hours with formed macrosporangia
            _macrosporangia_formed_daily += Output.outputsMisfits.macrosporangia_formed_hourly;


            //Check macrosporangia spread
            if (Output.outputsMisfits.are_macrosporangia_formed == 1)
            {
                Output.outputsMisfits.macrosporangia_spread_hourly = 
                    MacrosporangiaSpreadSuitability(Input, Parameters.misfitsParameters, Output.outputsMisfits);
            }
            else
            {
                Output.outputsMisfits.macrosporangia_spread_hourly = 0;
            }
            _macrosporangia_spread_daily += Output.outputsMisfits.macrosporangia_spread_hourly;


            //Check macrosporangia infection
            if (Output.outputsMisfits.are_macrosporangia_spread == 1)
            {
                Output.outputsMisfits.downyMildew_infection_hourly = MacrosporangiaInfectionSuitability(Input,
                    Parameters.misfitsParameters, Output.outputsMisfits);
            }
            else
            {
                Output.outputsMisfits.downyMildew_infection_hourly = 0;
                Output.outputsMisfits.macrosporangia_infection_temp_moisture_function = 0;
            }
            _downyMildew_infection_daily += Output.outputsMisfits.downyMildew_infection_hourly;


            #region Integrate daily values
            if (Input.Date.Hour == 23)
            {
                Output.outputsMisfits.macrosporangia_suitability_daily += _counter_macrosporangia_suitability;
                //Macrosporangia formation
                Output.outputsMisfits.macrosporangia_formed_daily += _macrosporangia_formed_daily;
                //misfitsOut spread
                Output.outputsMisfits.macrosporangia_spread_daily += _macrosporangia_spread_daily;
                //Primary infections
                Output.outputsMisfits.downyMildew_infection_daily += _downyMildew_infection_daily;
            }
            #endregion


            if (Output.outputsMisfits.downyMildew_infection_hourly == 1)
            {
                var infectionEvent = new misfitsInfectionEvent();
                infectionEvent.germinationDate = Input.Date;
                infectionEvent.infectionDate = Input.Date;
                Output.outputsMisfits.infectionEvents.Add(infectionEvent);
                infectionEvent.idInfection = Output.outputsMisfits.infectionEvents.Count();
            }
        }
        #endregion

        #region intermediate functions
        private void ReinitializeVariablesAtHour0(int hour, OutputsMisfits outputsMisfits)
        {
            if (hour == 0)
            {
                outputsMisfits.macrosporangia_spread_daily = 0;
                outputsMisfits.macrosporangia_formed_daily = 0;
                outputsMisfits.downyMildew_infection_hourly = 0;
                outputsMisfits.macrosporangia_suitability_daily = 0;
                _macrosporangia_formed_daily = 0;
                _macrosporangia_spread_daily = 0;
                _downyMildew_infection_daily = 0;
                _counter_macrosporangia_suitability = 0;
            }
        }

        private int MacrosporangiaFormationSuitability(Input Input, parametersMisfits _parameters, OutputsMisfits outputsMisfits)
        {
            /*from METOS website
			 This is the case as long as the leaves are wet, 
			 or the relative humidity after the rain does not fall below 70%.*/

            int MacrosporangiaSuitability = 0;

            //check if it rained
            if (Input.Precipitation > 0)
            {
                outputsMisfits.is_rained = 1;
            }

            //if there is leaf wetness, macrosporangia suitability is true
            if (Input.LeafWetness == 1)
            {
                MacrosporangiaSuitability = 1;
                //when there is leaf wetness, the "boolean" is_rained is false
                if (Input.Precipitation == 0)
                {
                    outputsMisfits.is_rained = 0;
                }
            }
            //if the leaves are dry, then check if it rained, and RH is still above a threshold (70%)
            else
            {
                if (Input.Precipitation > 0 ||
                    outputsMisfits.is_rained == 1 && Input.RelativeHumidity > _parameters.RHminMacrosporangiaFormation)
                {
                    MacrosporangiaSuitability = 1;
                }
                //if it did not rain, macrosporangia suitability is false
                else
                {
                    MacrosporangiaSuitability = 0;
                    outputsMisfits.is_rained = 0;
                }
            }

            //Macrosporangia suitability, true/false
            return MacrosporangiaSuitability;
        }

        private int MacrosporangiaSpreadSuitability(Input Input, parametersMisfits _parameters, OutputsMisfits outputsMisfits)
        {
            int MacrosporangiaSpread = 0;

            //check if it rained
            if (Input.Precipitation > 0)
            {
                outputsMisfits.macrosporangia_precipitation_sum += Input.Precipitation;
            }
            else
            {
                outputsMisfits.macrosporangia_precipitation_sum = 0;

            }

            if (outputsMisfits.macrosporangia_precipitation_sum >= _parameters.RainStartMacrosporangiaSpread)
            {
                outputsMisfits.macrosporangia_formed_hourly = 0;
                MacrosporangiaSpread = 1;
                outputsMisfits.macrosporangia_precipitation_sum = 0;
                outputsMisfits.are_macrosporangia_spread = 1;
            }

            return MacrosporangiaSpread;
        }

        private int MacrosporangiaInfectionSuitability(Input Input,
            parametersMisfits _parameters, OutputsMisfits outputsMisfits)
        {
            int MacrosporangiaSuitability = 0;

            if (Input.Temperature < _parameters.TminMacrosporangiaInfection ||
                Input.Temperature > _parameters.TmaxMacrosporangiaInfection)
            {
                outputsMisfits.macrosporangia_infection_temp_moisture_function = 0;
            }
            else
            {
                double Tfunction = temperature_function(Input.Temperature,
                    _parameters.TmaxMacrosporangiaInfection, _parameters.TminMacrosporangiaInfection, _parameters.ToptMacrosporangiaInfection);

                outputsMisfits.macrosporangia_infection_temp_moisture_function = _parameters.LWminMacrosporangiaInfection /
                    Tfunction;
            }


            //check if there is leaf wetness
            if (Input.LeafWetness > 0)
            {
                outputsMisfits.counter_leafwetness_macrosporangia++;
            }
            else
            {
                outputsMisfits.counter_leafwetness_macrosporangia = 0;
                outputsMisfits.are_macrosporangia_spread = 0;
            }
            //check conditions for primary infection
            if ((outputsMisfits.counter_leafwetness_macrosporangia >= outputsMisfits.macrosporangia_infection_temp_moisture_function ||
                outputsMisfits.counter_leafwetness_macrosporangia >= _parameters.LWoptMacrosporangiaFormation) &&
                outputsMisfits.macrosporangia_infection_temp_moisture_function != 0)
            {
                MacrosporangiaSuitability = 1;
                outputsMisfits.counter_leafwetness_macrosporangia = 0;
                outputsMisfits.are_macrosporangia_spread = 0;
                outputsMisfits.are_macrosporangia_formed = 0;
                outputsMisfits.downyMildew_infection_hourly = 1;

            }



            return MacrosporangiaSuitability;


        }

        private double MacrosporangiaMoistureFunction(OutputsMisfits outputsMisfits, parametersMisfits _parameters)
        {
            //local variable
            int MacrosporangiaMoistureSuitability = 0;

            //check if counter of suitability is higher then non-limiting leaf wetness
            if (outputsMisfits.macrosporangia_temperature_function > 0 &&
                outputsMisfits.macrosporangia_suitability_hourly >= _parameters.LWoptMacrosporangiaFormation)
            {
                MacrosporangiaMoistureSuitability = 1;
            }
            //check if counter of suitability is below minimum leaf wetness
            else if (outputsMisfits.macrosporangia_temperature_function == 0 ||
                outputsMisfits.macrosporangia_suitability_hourly < _parameters.LWminMacrosporangiaFormation)
            {
                MacrosporangiaMoistureSuitability = 0;
            }
            //check the condition of suitability based on LW and T requirement
            else
            {
                double MoistureFunction = _parameters.LWminMacrosporangiaFormation / outputsMisfits.macrosporangia_temperature_function;
                if (MoistureFunction < 0)
                {
                    MoistureFunction = 0;
                }

                if (outputsMisfits.macrosporangia_suitability_hourly > MoistureFunction)
                {
                    MacrosporangiaMoistureSuitability = 1;
                }
                else
                {
                    MacrosporangiaMoistureSuitability = 0;
                }
            }


            return MacrosporangiaMoistureSuitability;

        }

        private double temperature_function(double Temperature, double Tmax,
       double Tmin, double Topt)
        {
            double Tfunction = 0;

            if (Temperature < Tmin || Temperature > Tmax)
            {
                Tfunction = 0;
            }
            else
            {
                double firstTerm = (Tmax - Temperature) /
                        (Tmax - Topt);
                double secondTerm = (Temperature - Tmin) /
                                     (Topt - Tmin);
                double Exponential = (Topt - Tmin) /
                                     (Tmax - Topt);

                Tfunction = firstTerm * Math.Pow(secondTerm, Exponential);
            }
            return Tfunction;
        }
        #endregion
    }
}
