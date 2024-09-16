using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Crm.Sdk.Messages;

namespace bolt_Clone_Rental_Cost_Sheet
{
    public class bolt_CloneRentalCostSheet : IPlugin
    {
        /// <summary>
        /// A plugin that clones the Rental Cost Sheet and its child records. BCEW version
        /// </summary>
        /// <remarks>
        /// Entity: bolt_rentalcostsheet(Rental Cost Sheet)
        /// Message: Custom Action - bolt_CloneRentalCostSheet
        /// Stage: Post Operation
        /// Mode: Synchronous
        /// Other: Action triggered on custom button click
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
                context.InputParameters["Target"] is EntityReference target)
            {
                // Obtain the organization service reference which you will need for  
                // web service calls.  
                IOrganizationServiceFactory serviceFactory =
                    (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

                try
                {
                    #region Main logic to create new cost sheet and related entities
                    // Obtain the target entity from the input parameters.  
                    EntityReference cost_sheet_ref = target;
                    // Set entity type name from target
                    string primary_entity_type = cost_sheet_ref.LogicalName;

                    if (primary_entity_type == "bolt_rentalcostsheet")
                    {
                        Entity cost_sheet = Retrieve_CostSheetandRelatedRecords(cost_sheet_ref);

                        tracingService.Trace("CloneRentalCostSheetPlugin: Updating Cost Sheet before clone");

                        // Update cost sheet clone entity
                        cost_sheet.Id = Guid.Empty;
                        cost_sheet.Attributes.Remove("bolt_rentalcostsheetid");
                        cost_sheet.Attributes.Remove("bolt_quotenumber");
                        cost_sheet.Attributes["bolt_name"] = cost_sheet.GetAttributeValue<string>("bolt_proposalname") + " - COPY";
                        cost_sheet.Attributes["bolt_proposalname"] = cost_sheet.GetAttributeValue<string>("bolt_proposalname") + " - COPY";
                        cost_sheet.EntityState = null;
                        //cost_sheet_clone.Attributes.Remove("statecode");
                        //cost_sheet_clone.Attributes.Remove("statuscode");
                        
                        tracingService.Trace("CloneRentalCostSheetPlugin: Updating related records before clone");

                        // Updates for related entities
                        foreach (var kvp in cost_sheet.RelatedEntities)
                        {
                            foreach (var record in kvp.Value.Entities)
                            {
                                record.Id = Guid.Empty;
                                record.Attributes.Remove(record.LogicalName.ToLower() + "id");
                                //record.Attributes.Remove("bolt_relatedcostsheetid");
                                record.EntityState = null;
                                //record.Attributes.Remove("statecode");
                                //record.Attributes.Remove("statuscode");
                            }
                        }

                        // CreateRequest with updated Cost Sheet as target
                        CreateRequest request = new CreateRequest() 
                        {
                            Target = cost_sheet
                        };

                        tracingService.Trace("CloneRentalCostSheetPlugin: Starting create request for Cost Sheet and related entities");

                        // Execute CreateRequest to create copy of Cost Sheet and related records
                        CreateResponse response = (CreateResponse)service.Execute(request);
                        
                        // Create EntityReference for newly created clone cost sheet
                        EntityReference clone_cost_sheet_ref = new EntityReference(primary_entity_type, response.id);
                        
                        // Set OutputParameter for cloned cost sheet EntityReference - output parameter used to load record in new tab once plugin/action finish
                        context.OutputParameters["ClonedCostSheet"] = clone_cost_sheet_ref;
                        //context.OutputParameters["ClonedCostSheet"] = new EntityReference();
                        #endregion

                        #region Update original cost sheet, set Primary = No
                        // Retrieve original cost sheet since original values were overwritten
                        Entity original_cost_sheet = service.Retrieve(primary_entity_type, cost_sheet_ref.Id, new ColumnSet("bolt_primary"));
                        
                        // If bolt_primary = Yes, set to No
                        if (original_cost_sheet.GetAttributeValue<bool>("bolt_primary"))
                        {
                            // Set primary to No
                            original_cost_sheet.Attributes["bolt_primary"] = false;
                        
                            // Update cloned Cost Sheet
                            service.Update(original_cost_sheet);
                        }
                        #endregion
                        //
                        //#region Update Rollup Fields of new cost sheet record
                        //
                        ////// Call action to update rollup fields, instead of previous approach (commented out below). Results may be equavalent in some cases.
                        //
                        // OPTION ONE
                        // Calling the Action
                        OrganizationRequest req = new OrganizationRequest("bolt_ACT_RentalCostSheetrollupautorecalc");
                        req["Target"] = new EntityReference(primary_entity_type, clone_cost_sheet_ref.Id);
                        service.Execute(req);

                        OrganizationRequest req2 = new OrganizationRequest("bolt_ACT_RentalCostSheetrollupautorecalc");
                        req2["Target"] = new EntityReference(primary_entity_type, original_cost_sheet.Id);
                        service.Execute(req2);

                        //// OPTION TWO
                        ////// Create list of Cost Sheet rollup field names to update
                        ////List<string> rollup_fields = new List<string>()
                        ////{
                        ////    "bolt_totalcablecost",
                        ////    "bolt_totalcableprice",
                        ////    "bolt_totalgenrentalcost",
                        ////    "bolt_totalgenrentalprice",
                        ////    "bolt_totallaborcost",
                        ////    "bolt_totallaborprice",
                        ////    "bolt_totalmisccost",
                        ////    "bolt_totalmiscprice",
                        ////};
                        ////// Initialize new multiple request
                        ////ExecuteMultipleRequest multi_request = new ExecuteMultipleRequest() 
                        ////{
                        ////    // Assign settings that define execution behavior: continue on error, return responses. 
                        ////    Settings = new ExecuteMultipleSettings()
                        ////    {
                        ////        ContinueOnError = false,
                        ////        ReturnResponses = true,
                        ////    },
                        ////    // Create an empty organization request collection.
                        ////    Requests = new OrganizationRequestCollection()
                        ////};
                        ////
                        ////// Create and add Calculated Rollup Field Requests for each rollup field in list above
                        ////foreach (string field in rollup_fields) 
                        ////{
                        ////    CalculateRollupFieldRequest rollup_request = new CalculateRollupFieldRequest()
                        ////    {
                        ////        Target = cloned_cost_sheet_ref,
                        ////        FieldName = field
                        ////    };
                        ////
                        ////    multi_request.Requests.Add(rollup_request);
                        ////}
                        ////
                        ////// Execute request and return response
                        ////ExecuteMultipleResponse rollup_response = (ExecuteMultipleResponse)service.Execute(multi_request);
                        ////#endregion
                    }
                }

                catch (FaultException<OrganizationServiceFault> ex)
                {
                    throw new InvalidPluginExecutionException("An error occurred in CloneRentalCostSheetPlugin.", ex);
                }

                catch (Exception ex)
                {
                    tracingService.Trace("CloneRentalCostSheetPlugin: {0}", ex.ToString());
                    throw;
                }

                // Helper functions
                // Retrieves the primary reference record and its related records
                Entity Retrieve_CostSheetandRelatedRecords(EntityReference cost_sheet_ref)
                {
                    // Initialize new request
                    var retrieveRequest = new RetrieveRequest
                    {
                        // Set cost sheet as target record
                        Target = cost_sheet_ref,

                        // The columns to retrieve for the parent record
                        ColumnSet = new ColumnSet(true),

                        // Initialize
                        RelatedEntitiesQuery = new RelationshipQueryCollection()
                    };

                    // Create list of string arrays containing related entity and associated relationship
                    List<string[]> entitytype_relationship_list = new List<string[]>
                    {
                        new string[2] { "bolt_rentalgenerators", "bolt_bolt_rentalcostsheet_bolt_rentalgenerator" },
                        new string[2] { "bolt_rentalcables", "bolt_bolt_rentalcostsheet_bolt_rentalcables" },
                        new string[2] { "bolt_rentallabor", "bolt_bolt_rentalcostsheet_bolt_rentallabor" },
                        new string[2] { "bolt_rentalmisc", "bolt_bolt_rentalcostsheet_bolt_rentalmisc" },
                        new string[2] { "bolt_rentalfreight", "bolt_bolt_rentalcostsheet_bolt_rentalfreight_RelatedCostSheet" }
                    };

                    // Add RelationshipQueryCollection for each related entity type
                    foreach (string[] array in entitytype_relationship_list)
                    {
                        // Create new QueryExpression
                        QueryExpression query = new QueryExpression(array[0])
                        {
                            // Get all columns from CostSheet
                            ColumnSet = new ColumnSet(true)
                        };

                        retrieveRequest.RelatedEntitiesQuery.Add(new Relationship(array[1]), query);
                    }

                    // Execute the request and retrieve the response
                    RetrieveResponse response = (RetrieveResponse)service.Execute(retrieveRequest);

                    Entity record = response.Entity;
                    return record;
                }
            }
        }
    }
}
