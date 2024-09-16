using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace BOLT.NPS.Rental.Inventory
{
    public class RentalCables_UpdateCablesOnHand : IPlugin
    {
        /// <summary>
        /// A plugin that updates the Cables On-hand value for Cable type Rental Inventory records.
        /// </summary>
        /// <remarks>
        /// Entity: bolt_rentalcables (Rental Cables)
        /// Message: Update
        /// Stage: Post Operation
        /// Mode: Synchronous
        /// Image: post_image - bolt_cableinventory
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
            if (context.InputParameters.Contains("Target") &&
                context.InputParameters["Target"] is Entity target)
            {
                // Obtain the organization service reference which you will need for web service calls.  
                IOrganizationServiceFactory serviceFactory =
                    (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

                // main
                try
                {
                    // Get the target Entity Reference from the input parameters.
                    EntityReference rental_inv_ref = context.PostEntityImages["post_image"].GetAttributeValue<EntityReference>("bolt_cableinventory");

                    if (rental_inv_ref != null)
                    {
                        // Define columns to retrieve
                        ColumnSet columnSet = new ColumnSet(
                            "bolt_cablename",
                            "bolt_cabletype",
                            "bolt_inventorytype",
                            "bolt_lastupdatedonhand",
                            "bolt_quantityonhand",
                            "bolt_quantitytotal",
                            "bolt_shop"
                        );

                        // Retrieve the Entity from target Entity Reference.
                        Entity rental_inv = service.Retrieve("bolt_rentalinventory", rental_inv_ref.Id, columnSet);

                        // Get today's date
                        DateTime todays_date = DateTime.Today;

                        // Get Inventory details
                        //int shop_value = rental_inv.GetAttributeValue<OptionSetValue>("bolt_shop").Value;
                        //int cable_type = rental_inv.GetAttributeValue<OptionSetValue>("bolt_cabletype").Value;
                        int total_quantity = rental_inv.GetAttributeValue<int>("bolt_quantitytotal");

                        EntityCollection rental_cables = Retrieve_Rental_Cables_on_site(todays_date, rental_inv_ref);

                        int cables_in_field = 0;
                        foreach (Entity cable in rental_cables.Entities)
                        {
                            cables_in_field += cable.GetAttributeValue<int>("bolt_quantity");
                        }

                        int cables_on_hand = total_quantity - cables_in_field;
                        rental_inv["bolt_quantityonhand"] = cables_on_hand;

                        service.Update(rental_inv);
                    }
                }

                catch (FaultException<OrganizationServiceFault> ex)
                {
                    throw new InvalidPluginExecutionException("An error occurred in RentalCableInventory_on_hand_calculation Plugin.", ex);
                }

                catch (Exception ex)
                {
                    tracingService.Trace("RentalCableInventory_on_hand_calculation Plugin: {0}", ex.ToString());
                    throw;
                }

                // Helper functions
                EntityCollection Retrieve_Rental_Cables_on_site(DateTime today, EntityReference rental_inv)
                {
                    // Set Condition Values
                    var aa_bolt_rentalinventoryid = rental_inv.Id;

                    // Instantiate QueryExpression query
                    var query = new QueryExpression("bolt_rentalcables");

                    // Add columns to query.ColumnSet
                    query.ColumnSet.AddColumns(
                        "bolt_actualreturndate",
                        "bolt_actualdispatchdate",
                        "bolt_quantity",
                        "bolt_cableinventory",
                        "bolt_inventoryassigned",
                        "bolt_cableshift",
                        "bolt_rentaltypecables",
                        "bolt_rentalproduct",
                        "bolt_rentalcablesid");

                    // Add conditions to query.Criteria
                    query.Criteria.AddCondition("bolt_inventoryassigned", ConditionOperator.Equal, true);
                    query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);

                    // Add orders
                    query.AddOrder("bolt_rentalproduct", OrderType.Ascending);

                    // Add link-entity aa
                    var aa = query.AddLink("bolt_rentalinventory", "bolt_cableinventory", "bolt_rentalinventoryid");
                    aa.EntityAlias = "aa";

                    // Add conditions to aa.LinkCriteria
                    aa.LinkCriteria.AddCondition("bolt_rentalinventoryid", ConditionOperator.Equal, aa_bolt_rentalinventoryid);

                    // Retrieve query results
                    EntityCollection result = service.RetrieveMultiple(query);

                    return result;
                }
            }
        }
    }
}