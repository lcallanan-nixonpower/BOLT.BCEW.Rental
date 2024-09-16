using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace BOLT.NPS.Rental.CostSheetChildren
{
    public class CostSheetChildren_ForceRollupOnDelete : IPlugin
    {
        /// <summary>
        /// A plugin that force rollup fields to calculate on Delete of Cost Sheet child records.
        /// </summary>
        /// <remarks>
        /// Entity: bolt_rentalfreight (Rental Freight), bolt_rentalmisc (Rental Misc), bolt_rentallabor (Rental Labor)
        /// Message, Stage, Order, Mode: Delete, PostOperation, 1, Synchronous 
        /// Image: Pre Image, All Attributes
        /// </remarks>
        public void Execute(IServiceProvider serviceProvider)
        {
            // Obtain the tracing service
            ITracingService tracingService =
            (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // Obtain the execution context from the service provider.  
            IPluginExecutionContext context = (IPluginExecutionContext)
                serviceProvider.GetService(typeof(IPluginExecutionContext));

            // The InputParameters collection contains all the data passed in the message request.  
            if (context.PreEntityImages.Contains("pre_image") &&
                context.PreEntityImages["pre_image"] is Entity)
            {
                // Obtain the target entity from the input parameters.  
                Entity entity = (Entity)context.PreEntityImages["pre_image"];

                // Obtain the IOrganizationService instance which you will need for  
                // web service calls.  
                IOrganizationServiceFactory serviceFactory =
                    (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

                try
                {
                    if (entity.LogicalName == "bolt_rentalmisc") /// Misc rollup
                    {
                        if (entity.Attributes.Contains("bolt_relatedcostsheetid") && entity.GetAttributeValue<EntityReference>("bolt_relatedcostsheetid") != null)
                        {
                            EntityReference cost_sheet_ref = entity.GetAttributeValue<EntityReference>("bolt_relatedcostsheetid");

                            List<string> rollup_fields = new List<string>()
                            {
                                "bolt_totalmisccost",
                                "bolt_totalmiscprice"
                            };

                            force_rollups(cost_sheet_ref, rollup_fields);

                            // If Rental Misc record is a Fuel type, then update Rental Cost Sheet(Total Fueling Amount) "rollup" simple currency field
                            if (entity.Attributes.Contains("bolt_miscoptions"))
                            {
                                OptionSetValue misc_type = entity.GetAttributeValue<OptionSetValue>("bolt_miscoptions");

                                if (misc_type.Equals(new OptionSetValue(454890002)) || misc_type.Equals(new OptionSetValue(454890005)) || misc_type.Equals(new OptionSetValue(454890006)))
                                {
                                    // Query existing Fuel type records
                                    string fueling_avg_fetch = @"
                                        <fetch version='1.0' mapping='logical' aggregate='true'>
                                          <entity name='bolt_rentalmisc'>
                                            <attribute name='bolt_markeduprentalprice' alias='Sum' aggregate='sum' />
                                            <filter type='and'>
                                              <condition attribute='statecode' operator='eq' value='0' />
                                              <condition attribute='bolt_relatedcostsheetid' operator='eq' value='" + cost_sheet_ref.Id + @"' />
                                              <condition attribute='bolt_miscoptions' operator='in'>
                                                <value>454890002</value>
                                                <value>454890005</value>
                                                <value>454890006</value>
                                              </condition>
                                            </filter>
                                          </entity>
                                        </fetch>";

                                    EntityCollection fueling_avg_result = service.RetrieveMultiple(new FetchExpression(fueling_avg_fetch));

                                    // Get aggregate result representing Total Fueling Amount
                                    Money fueling_avg = (Money)((AliasedValue)fueling_avg_result.Entities[0]["Sum"]).Value;

                                    // Update Total Fueling Price for related Cost Sheet                                    
                                    Entity cost_sheet = new Entity("bolt_rentalcostsheet", cost_sheet_ref.Id);
                                    cost_sheet["bolt_totalfuelingprice"] = fueling_avg;

                                    service.Update(cost_sheet);
                                }
                            }
                        }
                    }
                    else if (entity.LogicalName == "bolt_rentallabor") /// Labor rollup
                    {
                        if (entity.Attributes.Contains("bolt_relatedcostsheetid") && entity.GetAttributeValue<EntityReference>("bolt_relatedcostsheetid") != null)
                        {
                            EntityReference cost_sheet_ref = entity.GetAttributeValue<EntityReference>("bolt_relatedcostsheetid");

                            List<string> rollup_fields = new List<string>()
                            {
                                "bolt_totallaborcost",
                                "bolt_totallaborprice"
                            };

                            force_rollups(cost_sheet_ref, rollup_fields);
                        }
                    }
                    else if (entity.LogicalName == "bolt_rentalfreight") /// Freight rollup
                    {
                        if (entity.Attributes.Contains("bolt_relatedcostsheet") && entity.GetAttributeValue<EntityReference>("bolt_relatedcostsheet") != null)
                        {
                            EntityReference cost_sheet_ref = entity.GetAttributeValue<EntityReference>("bolt_relatedcostsheet");

                            List<string> rollup_fields = new List<string>()
                            {
                                "bolt_freightcostrollup",
                                "bolt_freightpricerollup"
                            };

                            force_rollups(cost_sheet_ref, rollup_fields);
                        }
                    }

                    void force_rollups(EntityReference ent_ref, List<string> rollups)
                    {
                        foreach (string field in rollups)
                        {
                            CalculateRollupFieldRequest rollup_request = new CalculateRollupFieldRequest()
                            {
                                Target = ent_ref,
                                FieldName = field
                            };

                            service.Execute(rollup_request);
                        }
                    }
                }

                catch (FaultException<OrganizationServiceFault> ex)
                {
                    throw new InvalidPluginExecutionException("An error occurred in onDelete force rollup plugin for Rental child Cost Sheet records.", ex);
                }

                catch (Exception ex)
                {
                    tracingService.Trace("Rental Cost Sheet child onDelete rollup plugin: {0}", ex.ToString());
                    throw;
                }
            }
        }
    }
}
