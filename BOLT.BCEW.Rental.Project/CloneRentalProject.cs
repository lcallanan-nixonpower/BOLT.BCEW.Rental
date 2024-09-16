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

namespace BOLT.NPS.Rental.Project
{
    /// <summary>
    /// A plugin that clones the Rental Project and all Cost Sheets and their child records. BCEW version
    /// </summary>
    /// <remarks>
    /// Entity: bolt_rentalproject(Rental Project)
    /// Message: Custom Action - bolt_ACT_Rental_CloneProject
    /// Stage: Post Operation
    /// Mode: Synchronous
    /// Other: Action triggered on custom button click
    /// </remarks>
    public class CloneRentalProject : IPlugin
    {
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
                    // Obtain the target entity from the input parameters.
                    EntityReference project_ref = target;

                    // Set entity type name from target
                    string primary_entity_type = project_ref.LogicalName;

                    // Create relationship between Project and Costsheet
                    Relationship project_cs_relationship = new Relationship("bolt_bolt_rentalproject_bolt_rentalcostsheet");

                    // Field exemption list for project fields
                    List<string> project_exemptions = new List<string>
                    {
                        "bolt_rentalprojectid",
                        "cr6f5_quotenumber",
                        "bolt_jobnumber",
                        //"bolt_dateofrfq",
                        "bolt_daterentalinfosheetsent",
                        "bolt_daterentalinfosheetreceived",
                        "bolt_datequotesenttocustomer",
                        //"bolt_bidduedate",
                        "bolt_actualclosedate",
                        "bolt_poloireceiveddate",
                        "bolt_customerpo",
                        "overriddencreatedon",
                        "bolt_sfid"
                    };

                    // Field exemption list for cost sheet fields               ******For Loop for Cost Sheet not utilized
                    List<string> costsheet_exemptions = new List<string>
                    {
                        "bolt_rentalcostsheetid",
                        "bolt_quotenumber"
                    };


                    if (primary_entity_type == "bolt_rentalproject")
                    {
                        #region Main logic to create new Project and Cost Sheets
                        Entity clone_project = Retrieve_ProjectandCostSheet(project_ref, project_cs_relationship);

                        tracingService.Trace("CloneRentalProjectPlugin: Fn -> Retrieve_ProjectandCostSheet completed");

                        // Prepare project object as needed in order to clone entity object - need to go back and structure an exception list for fields NOT to be copied. There are more exceptions for projects vs. costsheets
                        foreach (var field in project_exemptions)
                        {
                            clone_project.Attributes.Remove(field);
                        }
                        clone_project.Id = Guid.Empty;
                        clone_project.Attributes["bolt_rentalname"] = clone_project.GetAttributeValue<string>("bolt_rentalname") + " - COPY";
                        clone_project.Attributes["bolt_dateofrfq"] = DateTime.Today;
                        clone_project.Attributes["bolt_bidduedate"] = DateTime.Today;
                        clone_project.EntityState = null;
                        //clone_project.Attributes.Remove("statecode");
                        //clone_project.Attributes.Remove("statuscode");

                        tracingService.Trace("CloneRentalProjectPlugin: Clone_project entity prepared");

                        EntityReference cost_sheet_entityref = new EntityReference("rental_costsheet");

                        // Logic for preparing cost sheet clones - Not used if no related cost sheets.
                        if (clone_project.RelatedEntities[project_cs_relationship].Entities.Count() > 0)
                        {
                            Entity[] cs_collection = clone_project.RelatedEntities[project_cs_relationship].Entities.ToArray();

                            foreach (Entity cs in cs_collection)
                            {
                                // Cost Sheet attached to the project to be cloned. Defining to query for child records.
                                cost_sheet_entityref = cs.ToEntityReference();

                                // Retrieve cost sheet with children
                                Entity clone_cost_sheet = Retrieve_CostSheetandRelatedRecords(cost_sheet_entityref);

                                // Prepare clone cost sheet entity 
                                clone_cost_sheet.Id = Guid.Empty;
                                clone_cost_sheet.Attributes.Remove("bolt_rentalcostsheetid");
                                //clone_cost_sheet.Attributes.Remove("bolt_relatedrentalprojectid");
                                clone_cost_sheet.Attributes.Remove("bolt_quotenumber");
                                clone_cost_sheet.EntityState = null;
                                //clone_cost_sheet.Attributes.Remove("statecode");
                                //clone_cost_sheet.Attributes.Remove("statuscode");

                                // Preparation for related entities
                                foreach (var kvp in clone_cost_sheet.RelatedEntities)
                                {
                                    foreach (var record in kvp.Value.Entities)
                                    {
                                        record.Id = Guid.Empty;
                                        record.Attributes.Remove(record.LogicalName.ToLower() + "id");
                                        //record.Attributes.Remove("bolt_relatedcostsheetid"); //**** This method for breaking lookup with original CS causes error in BCEW environment
                                        record.EntityState = null;
                                        //record.Attributes.Remove("statecode");
                                        //record.Attributes.Remove("statuscode");
                                    }
                                }

                                tracingService.Trace("CloneRentalProjectPlugin: Clone_cost_sheet child records prepared");

                                // Replace "cost_sheet" with "clone_cost_sheet" --- the clone has the related records
                                clone_project.RelatedEntities[project_cs_relationship].Entities.Remove(cs);
                                clone_project.RelatedEntities[project_cs_relationship].Entities.Add(clone_cost_sheet);

                                tracingService.Trace("CloneRentalProjectPlugin: Added prepared clone_cs to clone_project");
                            }
                        }

                        // CreateRequest with updated Project as target --- includes the primary related costsheet object which should include all the necessary child records
                        CreateRequest request = new CreateRequest()
                        {
                            Target = clone_project
                        };

                        tracingService.Trace("CloneRentalProjectPlugin: NEXT LINE -> Execute service request to create cloned entity.");

                        // Execute CreateRequest to create copy of Project, primary CS and children.
                        CreateResponse response = (CreateResponse)service.Execute(request);

                        // Create EntityReference for newly created cloned cost sheet
                        EntityReference clone_project_ref = new EntityReference(primary_entity_type, response.id);

                        // Set OutputParameter for cloned cost sheet EntityReference - output parameter used to load record in new tab once plugin/action finish
                        context.OutputParameters["ClonedProject"] = clone_project_ref;

                        #region Update Rollup Fields of new cost sheet record
                        // Check for existence of CS before forcing rollups to update
                        if (clone_project.RelatedEntities[project_cs_relationship].Entities.Count() > 0)
                        {
                            Entity clone_project_post = Retrieve_ProjectandCostSheet(clone_project_ref, project_cs_relationship);

                            foreach (Entity cs in clone_project_post.RelatedEntities[project_cs_relationship].Entities)
                            {
                                // Calling the Action for new CS
                                OrganizationRequest req = new OrganizationRequest("bolt_ACT_RentalCostSheetrollupautorecalc");
                                req["Target"] = new EntityReference(cs.LogicalName, cs.Id);
                                service.Execute(req);
                            }

                            Entity cloned_project = Retrieve_ProjectandCostSheet(project_ref, project_cs_relationship);

                            foreach (Entity cs in cloned_project.RelatedEntities[project_cs_relationship].Entities)
                            {
                                // Calling the Action for old CS
                                OrganizationRequest req2 = new OrganizationRequest("bolt_ACT_RentalCostSheetrollupautorecalc");
                                req2["Target"] = new EntityReference(cs.LogicalName, cs.Id);
                                service.Execute(req2);

                                ExecuteWorkflowRequest req3 = new ExecuteWorkflowRequest()
                                {
                                    WorkflowId = Guid.Parse("E30C8E78-1F0B-4A00-9FCB-53FD59E9758D"), //WF name: Rollup Revenue to Project
                                    EntityId = cs.Id
                                };

                                service.Execute(req3);
                            }
                        }
                        #endregion

                        tracingService.Trace("CloneRentalProjectPlugin: MAIN LOGIC FINISHED");
                        #endregion
                    }
                }

                catch (FaultException<OrganizationServiceFault> ex)
                {
                    throw new InvalidPluginExecutionException("An error occurred in CloneRentalProjectPlugin.", ex);
                }

                catch (Exception ex)
                {
                    tracingService.Trace("CloneRentalProjectPlugin: {0}", ex.ToString());
                    throw;
                }

                // Helper functions
                // Retrieves the primary reference record (Rental Project) and its Primary Cost Sheet.
                Entity Retrieve_ProjectandCostSheet(EntityReference project_ref, Relationship relationship)
                {
                    // Initialize new request
                    var retrieveRequest = new RetrieveRequest
                    {
                        // Set cost sheet as target record
                        Target = project_ref,

                        // The columns to retrieve for the parent record
                        ColumnSet = new ColumnSet(true),

                        // Initialize
                        RelatedEntitiesQuery = new RelationshipQueryCollection()
                    };

                    // Create new QueryExpression 
                    QueryExpression query = new QueryExpression("bolt_rentalcostsheet")
                    {
                        // Get all columns from CostSheet
                        ColumnSet = new ColumnSet(true)
                    };

                    retrieveRequest.RelatedEntitiesQuery.Add(relationship, query);

                    // Execute the request and retrieve the response
                    RetrieveResponse response = (RetrieveResponse)service.Execute(retrieveRequest);

                    Entity project = response.Entity;

                    return project;
                }

                // Retrieves the primary reference record and its related records
                Entity Retrieve_CostSheetandRelatedRecords(EntityReference cost_sheet_ref)
                {
                    // Initialize new request
                    var cs_retrieveRequest = new RetrieveRequest
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
                        QueryExpression cs_query = new QueryExpression(array[0])
                        {
                            // Get all columns from CostSheet
                            ColumnSet = new ColumnSet(true)
                        };

                        cs_retrieveRequest.RelatedEntitiesQuery.Add(new Relationship(array[1]), cs_query);
                    }

                    // Execute the request and retrieve the response
                    RetrieveResponse cs_response = (RetrieveResponse)service.Execute(cs_retrieveRequest);

                    Entity record = cs_response.Entity;
                    return record;
                }
            }
        }
    }
}