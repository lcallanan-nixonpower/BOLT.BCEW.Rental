using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Crm.Sdk.Messages;

namespace BOLT.NPS.Rental.CostSheetChildren
{
    /// <summary>
    /// A plugin that calculates the Cost and Price fields for the Rental Cables entity. BCEW version
    /// </summary>
    /// <remarks>
    /// Entity: bolt_rentalcables (Rental Cables)
    /// Message, Stage, Order, Mode: Create, PreOperation, 2, Synchronous 
    /// Message, Stage, Order, Mode: Delete, PostOperation, 1, Synchronous 
    /// Image: Pre Image, All Attributes
    /// Message, Stage, Order, Mode: Update, PreOperation, 2, Synchronous 
    /// Image: Pre Image, bolt_grossprofitpercentage, bolt_rentaltypegen, bolt_monthlycostinput, bolt_rerent, bolt_rentalshiftgen, bolt_singledaycostinput, bolt_totaldays, bolt_totalmonths, bolt_totalweeks, bolt_weeklycostinput
    /// </remarks>
    public class CableCostPrice_calculations : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Obtain the tracing service
            ITracingService tracingService =
            (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // Obtain the execution context from the service provider.  
            IPluginExecutionContext context = (IPluginExecutionContext)
                serviceProvider.GetService(typeof(IPluginExecutionContext));

            // Obtain the IOrganizationService instance which you will need for  
            // web service calls.  
            IOrganizationServiceFactory serviceFactory =
                (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            if (context.MessageName == "Delete")
            {
                try
                {
                    Entity pre_entity = (Entity)context.PreEntityImages["pre_image"];

                    EntityReference costsheet_entref = pre_entity.GetAttributeValue<EntityReference>("bolt_relatedcostsheetid");

                    CalculateRollupFieldRequest request = new CalculateRollupFieldRequest
                    {
                        Target = costsheet_entref,
                        FieldName = "bolt_totalcableprice"
                    };

                    service.Execute(request);
                }
                catch (FaultException<OrganizationServiceFault> ex)
                {
                    throw new InvalidPluginExecutionException("An error occurred in Delete step of Rental Cable Price Calculation plugin.", ex);
                }

                catch (Exception ex)
                {
                    tracingService.Trace("Delete step Rental Cable Price Calculation plugin: {0}", ex.ToString());
                    throw;
                }
            }

            // The InputParameters collection contains all the data passed in the message request.  
            if (context.InputParameters.Contains("Target") &&
                context.InputParameters["Target"] is Entity)
            {
                // Obtain the target entity from the input parameters.  
                Entity entity = (Entity)context.InputParameters["Target"];

                // Create blank entity, only set as pre_image for Update step
                Entity pre_entity = new Entity();
                if (context.MessageName == "Update")
                {
                    pre_entity = (Entity)context.PreEntityImages["pre_image"];
                }

                try
                {
                    // Get input values from Target or Pre Entity
                    // Interval
                    int interval_Value;
                    if (entity.Attributes.Contains("bolt_rentaltypecables"))
                    {
                        OptionSetValue rental_Interval = entity.GetAttributeValue<OptionSetValue>("bolt_rentaltypecables");
                        interval_Value = rental_Interval.Value;
                    }
                    else
                    {
                        OptionSetValue rental_Interval = pre_entity.GetAttributeValue<OptionSetValue>("bolt_rentaltypecables");
                        interval_Value = rental_Interval.Value;
                    }

                    // Quantity
                    int quantity = entity.Attributes.Contains("bolt_quantity") ?
                        entity.GetAttributeValue<int>("bolt_quantity") : pre_entity.GetAttributeValue<int>("bolt_quantity");

                    // Shift
                    int shift_Value;
                    if (entity.Attributes.Contains("bolt_cableshift"))
                    {
                        OptionSetValue rental_Shift = entity.GetAttributeValue<OptionSetValue>("bolt_cableshift");
                        shift_Value = rental_Shift.Value;
                    }
                    else
                    {
                        OptionSetValue rental_Shift = pre_entity.GetAttributeValue<OptionSetValue>("bolt_cableshift");
                        shift_Value = rental_Shift.Value;
                    }
                    var shift_multipliers = new Dictionary<int, decimal>()
                    {
                        {454890000, (decimal)1.0},  // Single
                        {454890001, (decimal)1.5},  // Double
                        {454890002, (decimal)2.0}   // Triple
                    };

                    // Re-rent
                    bool rerent = entity.Attributes.Contains("bolt_rerent") ?
                        entity.GetAttributeValue<bool>("bolt_rerent") : pre_entity.GetAttributeValue<bool>("bolt_rerent");

                    // Margin %
                    decimal margin_Percent = entity.Attributes.Contains("bolt_grossprofitpercentage") ?
                        entity.GetAttributeValue<decimal>("bolt_grossprofitpercentage") : pre_entity.GetAttributeValue<decimal>("bolt_grossprofitpercentage");

                    // Total Days,Weeks,Months
                    int total_Days = entity.Attributes.Contains("bolt_totaldays") ?
                        entity.GetAttributeValue<int>("bolt_totaldays") : pre_entity.GetAttributeValue<int>("bolt_totaldays");
                    int total_Weeks = entity.Attributes.Contains("bolt_totalweeks") ?
                        entity.GetAttributeValue<int>("bolt_totalweeks") : pre_entity.GetAttributeValue<int>("bolt_totalweeks");
                    int total_Months = entity.Attributes.Contains("bolt_totalmonths") ?
                        entity.GetAttributeValue<int>("bolt_totalmonths") : pre_entity.GetAttributeValue<int>("bolt_totalmonths");

                    // Base currency input fields
                    Money daily_input = entity.Attributes.Contains("bolt_singledaycostinput") ?
                        entity.GetAttributeValue<Money>("bolt_singledaycostinput") : pre_entity.GetAttributeValue<Money>("bolt_singledaycostinput");
                    decimal daily_input_value = daily_input.Value;

                    Money weekly_input = entity.Attributes.Contains("bolt_weeklycostinput") ?
                        entity.GetAttributeValue<Money>("bolt_weeklycostinput") : pre_entity.GetAttributeValue<Money>("bolt_weeklycostinput");
                    decimal weekly_input_value = weekly_input.Value;

                    Money monthly_input = entity.Attributes.Contains("bolt_monthlycostinput") ?
                        entity.GetAttributeValue<Money>("bolt_monthlycostinput") : pre_entity.GetAttributeValue<Money>("bolt_monthlycostinput");
                    decimal monthly_input_value = monthly_input.Value;


                    if (!rerent) // Price basis - inputs represent price to customer
                    {
                        decimal daily_price_value = daily_input_value * shift_multipliers[shift_Value] * quantity;
                        decimal weekly_price_value = weekly_input_value * shift_multipliers[shift_Value] * quantity;
                        decimal monthly_price_value = monthly_input_value * shift_multipliers[shift_Value] * quantity;

                        decimal daily_cost_value = daily_price_value * (1 - margin_Percent / 100);
                        decimal weekly_cost_value = weekly_price_value * (1 - margin_Percent / 100);
                        decimal monthly_cost_value = monthly_price_value * (1 - margin_Percent / 100);

                        setvalues(daily_cost_value, weekly_cost_value, monthly_cost_value, daily_price_value, weekly_price_value, monthly_price_value);
                    }
                    else // Cost basis - inputs represent costs to company
                    {
                        decimal daily_cost_value = daily_input_value * shift_multipliers[shift_Value] * quantity;
                        decimal weekly_cost_value = weekly_input_value * shift_multipliers[shift_Value] * quantity;
                        decimal monthly_cost_value = monthly_input_value * shift_multipliers[shift_Value] * quantity;

                        decimal daily_price_value = daily_cost_value / (1 - margin_Percent / 100);
                        decimal weekly_price_value = weekly_cost_value / (1 - margin_Percent / 100);
                        decimal monthly_price_value = monthly_cost_value / (1 - margin_Percent / 100);

                        setvalues(daily_cost_value, weekly_cost_value, monthly_cost_value, daily_price_value, weekly_price_value, monthly_price_value);
                    }

                    void setvalues(decimal dcv, decimal wcv, decimal mcv, decimal dpv, decimal wpv, decimal mpv)
                    {
                        entity["tb_dailycost"] = new Money(dcv);
                        entity["tb_weeklycost"] = new Money(wcv);
                        entity["tb_monthlycost"] = new Money(mcv);
                        entity["tb_dailyprice"] = new Money(dpv);
                        entity["tb_weeklyprice"] = new Money(wpv);
                        entity["tb_monthlyprice"] = new Money(mpv);

                        decimal Total_Price;
                        decimal Total_Cost;
                        decimal Margin;

                        switch (interval_Value)
                        {
                            case 454890003: // Date Range - Assign Total Cost/Price based on calculated total days,weeks,months (which are based on the selected dates)
                                if (total_Days > 0 || total_Weeks > 0 || total_Months > 0)
                                {
                                    // Compare Total Daily vs Weekly Price, if greater than, add week and set Total Days to zero
                                    decimal Total_Daily_value = dpv * total_Days;
                                    if (Total_Daily_value >= wpv) { total_Weeks++; total_Days = 0; }

                                    // Compare Total Daily+Total Weekly vs Monthly Price, if greater than, add month and set Total Days and Total Weeks to zero.
                                    // recalculate Total_Daily_value in case it was set to zero
                                    Total_Daily_value = dpv * total_Days;
                                    decimal Total_DailyWeekly_value = Total_Daily_value + (wpv * total_Weeks);
                                    if (Total_DailyWeekly_value >= mpv) { total_Months++; total_Weeks = 0; total_Days = 0; }

                                    Total_Price = (dpv * total_Days) + (wpv * total_Weeks) + (mpv * total_Months);
                                    Total_Cost = (dcv * total_Days) + (wcv * total_Weeks) + (mcv * total_Months);
                                    Margin = Total_Price - Total_Cost;

                                    entity["tb_totalcost"] = new Money(Total_Cost);
                                    entity["tb_totalprice"] = new Money(Total_Price);
                                    entity["tb_grossprofit"] = new Money(Margin);
                                }
                                else
                                {
                                    throw new Exception(); //warn user of missing D,W,M totals
                                }

                                break;

                            case 454890000: // Daily - Assign Total Cost/Price based on daily values
                                Total_Price = dpv;
                                Total_Cost = dcv;
                                Margin = Total_Price - Total_Cost;

                                entity["tb_totalcost"] = new Money(Total_Cost);
                                entity["tb_totalprice"] = new Money(Total_Price);
                                entity["tb_grossprofit"] = new Money(Margin);
                                break;

                            case 454890001: // Weekly - Assign Total Cost/Price based on weekly values
                                Total_Price = wpv;
                                Total_Cost = wcv;
                                Margin = Total_Price - Total_Cost;

                                entity["tb_totalcost"] = new Money(Total_Cost);
                                entity["tb_totalprice"] = new Money(Total_Price);
                                entity["tb_grossprofit"] = new Money(Margin);
                                break;

                            case 454890002: // Monthly - Assign Total Cost/Price based on monthly values
                                Total_Price = mpv;
                                Total_Cost = mcv;
                                Margin = Total_Price - Total_Cost;

                                entity["tb_totalcost"] = new Money(Total_Cost);
                                entity["tb_totalprice"] = new Money(Total_Price);
                                entity["tb_grossprofit"] = new Money(Margin);
                                break;
                        }
                    }
                }

                catch (FaultException<OrganizationServiceFault> ex)
                {
                    throw new InvalidPluginExecutionException("An error occurred in Rental Cable Price Calculation plugin.", ex);
                }

                catch (Exception ex)
                {
                    tracingService.Trace("Rental Cable Price Calculation plugin: {0}", ex.ToString());
                    throw;
                }
            }
        }
    }
}