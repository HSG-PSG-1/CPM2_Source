﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic;
using System.Data.Linq.SqlClient;
using CPM.DAL;
using CPM.Helper;

namespace CPM.Services
{
    public class DashboardService : _ServiceBase
    {
        #region Variables

        public readonly vw_Claim_Dashboard emptyView = new vw_Claim_Dashboard() { ID = Defaults.Integer };
        public const string sortOn = "ClaimNo DESC", sortOn1 = "ClaimNo DESC";

        #endregion
                
        public List<vw_Claim_Dashboard> SearchKO(string orderBy, int? pgIndex, int pageSize, vw_Claim_Dashboard das, bool isExcelReport, bool applyLocFilter)
        {
            orderBy = string.IsNullOrEmpty(orderBy) ? sortOn : orderBy;

            using (dbc)
            {
                IQueryable<vw_Claim_Dashboard> dasQ;
                #region Special case for customer (apply accessible location filter)
                if (!applyLocFilter)
                    dasQ = (from vw_u in dbc.vw_Claim_Dashboards select vw_u);
                else // only for customer
                    dasQ = (from vw_u in dbc.vw_Claim_Dashboards
                            join ul in dbc.UserLocations on new { LocID = vw_u.ShipToLocationID } equals new { LocID = ul.LocID }
                            where ul.UserID == _SessionUsr.ID
                            select vw_u);
                #endregion

                //Get filters - if any
                dasQ = PrepareQuery(dasQ, das);
                // Apply Sorting, Pagination and return PagedList

                #region Sort and Return result
                //Special case to replace Customproperty with original (for ShipToLoc)
                // For better implementation: SO: 2241643/how-to-use-a-custom-property-in-a-linq-to-entities-query
                orderBy = (orderBy ?? "").Replace("ShipToLocAndCode", "ShipToLoc");

                if (isExcelReport)
                    return dasQ.OrderBy(orderBy).ToList<vw_Claim_Dashboard>();
                else /* Apply pagination and return - kept for future ref */
                    return dasQ.OrderBy(orderBy).Skip(pgIndex.Value * pageSize).Take(pageSize).ToList<vw_Claim_Dashboard>();

                #endregion
            }
        }

        public static IQueryable<vw_Claim_Dashboard> PrepareQuery(IQueryable<vw_Claim_Dashboard> dasQ, vw_Claim_Dashboard das)
        {
            #region Append WHERE clause if applicable

            dasQ = dasQ.Where(o => o.Archived == das.Archived);

            if (!string.IsNullOrEmpty(das.ClaimNos))// Filter for multiple Claim No.s
            {
                int SingleClaimNo = -1;

                if (int.TryParse(das.ClaimNos, out SingleClaimNo))
                    dasQ = dasQ.Where(o => SqlMethods.Like(o.ClaimNo.ToString(), "%" + SingleClaimNo.ToString() + "%"));
                else
                    dasQ = dasQ.Where(o => Defaults.stringToIntList(das.ClaimNos).Contains(o.ClaimNo));
            }

            if (!string.IsNullOrEmpty(das.CustRefNo))
                dasQ = dasQ.Where(o => SqlMethods.Like(o.CustRefNo.ToLower(), das.CustRefNo.ToLower()));

            if (das.BrandID > 0) dasQ = dasQ.Where(o => o.BrandID == das.BrandID);
            else if (!string.IsNullOrEmpty(das.BrandName))
                dasQ = dasQ.Where(o => SqlMethods.Like(o.BrandName.ToLower(), "%" + das.BrandName.ToLower() + "%"));

            if (das.StatusID > 0) dasQ = dasQ.Where(o => o.StatusID == das.StatusID);
            else if (!string.IsNullOrEmpty(das.Status))
                dasQ = dasQ.Where(o => SqlMethods.Like(o.Status.ToLower(), das.Status.ToLower()));

            if (das.AssignedTo > 0) dasQ = dasQ.Where(o => o.AssignedTo == das.AssignedTo);
            if (das.VendorID > 0) dasQ = dasQ.Where(o => o.VendorID == das.VendorID);

            if (das.CustID > 0) dasQ = dasQ.Where(o => o.CustID == das.CustID);
            else if (!string.IsNullOrEmpty(das.CustOrg))
                dasQ = dasQ.Where(o => SqlMethods.Like(o.CustOrg.ToLower(), das.CustOrg.ToLower()));

            if (das.SalespersonID > 0) dasQ = dasQ.Where(o => o.SalespersonID == das.SalespersonID);
            else if (!string.IsNullOrEmpty(das.Salesperson))
                dasQ = dasQ.Where(o => SqlMethods.Like(o.Salesperson.ToLower(), das.Salesperson.ToLower()));

            if (das.ShipToLocationID > 0) dasQ = dasQ.Where(o => o.ShipToLocationID == das.ShipToLocationID);
            else if (!string.IsNullOrEmpty(das.ShipToLoc)) dasQ = dasQ.Where
               (o => SqlMethods.Like(o.ShipToLoc.ToLower(), das.ShipToLoc.ToLower()));

            //Apply date filter
            //http://www.filamentgroup.com/lab/date_range_picker_using_jquery_ui_16_and_jquery_ui_css_framework/
            if (das.ClaimDateFrom.HasValue)
                dasQ = dasQ.Where(o => o.ClaimDate.Date >= das.ClaimDateFrom_SQL.Value.Date);
            if (das.ClaimDateTo.HasValue)
                dasQ = dasQ.Where(o => o.ClaimDate.Date <= das.ClaimDateTo_SQL.Value.Date);

            #endregion

            return dasQ;
        }

        public List<vw_ClaimWithItemDetail> ClaimWithDetails()
        {
            using (dbc)
            {
                IQueryable<vw_ClaimWithItemDetail> dasQ =
                    (from vw_u in dbc.vw_ClaimWithItemDetails orderby vw_u.ClaimID, vw_u.CDID select vw_u);
                
                //Get filters - if any
                //dasQ = PrepareQuery(dasQ, das);
                // Apply Sorting, Pagination and return PagedList

                //sorting already applied by the view
                return dasQ.ToList<vw_ClaimWithItemDetail>();                
            }
        }

        public List<vw_ClaimWithItemDetail> ClaimWithDetails(vw_ClaimWithItemDetail das, int? pgIndex, int pageSize)
        { // No location or other filters applied yet!
            using (dbc)
            {
                IQueryable<vw_ClaimWithItemDetail> dasQ =
                    (from vw_u in dbc.vw_ClaimWithItemDetails orderby vw_u.ClaimID, vw_u.CDID select vw_u);

                //Get filters - if any
                dasQ = PrepareReportQuery(dasQ, das);
                // Apply Sorting, Pagination and return PagedList

                //sorting already applied by the view
                return dasQ.ToList<vw_ClaimWithItemDetail>();
            }
        }

        public static IQueryable<vw_ClaimWithItemDetail> PrepareReportQuery(IQueryable<vw_ClaimWithItemDetail> dasQ, vw_ClaimWithItemDetail das)
        {
            #region Append WHERE clause if applicable

            //dasQ = dasQ.Where(o => o.Archived == das.Archived);

            if (!string.IsNullOrEmpty(das.ClaimNos))// Filter for multiple Claim No.s
            {
                int SingleClaimNo = -1;

                if (int.TryParse(das.ClaimNos, out SingleClaimNo))
                    dasQ = dasQ.Where(o => SqlMethods.Like(o.ClaimNo.ToString(), "%" + SingleClaimNo.ToString() + "%"));
                else
                    dasQ = dasQ.Where(o => Defaults.stringToIntList(das.ClaimNos).Contains(o.ClaimNo));
            }

            if (!string.IsNullOrEmpty(das.CustRefNo))
                dasQ = dasQ.Where(o => SqlMethods.Like(o.CustRefNo.ToLower(), das.CustRefNo.ToLower()));

            if (das.BrandID > 0) dasQ = dasQ.Where(o => o.BrandID == das.BrandID);
            else if (!string.IsNullOrEmpty(das.Brand))
                dasQ = dasQ.Where(o => SqlMethods.Equals(o.Brand.ToLower(), das.Brand.ToLower()));
            
            if (das.StatusID > 0) dasQ = dasQ.Where(o => o.StatusID == das.StatusID);
            if (das.AssignedTo > 0) dasQ = dasQ.Where(o => o.AssignedTo == das.AssignedTo);
            else if (!string.IsNullOrEmpty(das.AssignToName))
                dasQ = dasQ.Where(o => SqlMethods.Equals(o.AssignToName.ToLower(), das.AssignToName.ToLower()));
            //if (das.VendorID > 0) dasQ = dasQ.Where(o => o.VendorID == das.VendorID);

            if (das.CustID > 0) dasQ = dasQ.Where(o => o.CustID == das.CustID);
            else if (!string.IsNullOrEmpty(das.CustOrgName))
                dasQ = dasQ.Where(o => SqlMethods.Equals(o.CustOrgName.ToLower(), das.CustOrgName.ToLower()));
            
            if (das.SalespersonID > 0) 
                dasQ = dasQ.Where(o => o.SalespersonID == das.SalespersonID);
            else if (!string.IsNullOrEmpty(das.Salesperson))
                dasQ = dasQ.Where(o => SqlMethods.Like(o.Salesperson.ToLower(), das.Salesperson.ToLower()));

            if (das.ShipToLocationID > 0) dasQ = dasQ.Where(o => o.SalespersonID == das.SalespersonID);
            
            //Apply date filter
            //http://www.filamentgroup.com/lab/date_range_picker_using_jquery_ui_16_and_jquery_ui_css_framework/
            if (das.ClaimDateFrom.HasValue)
                dasQ = dasQ.Where(o => o.ClaimDate.Date >= das.ClaimDateFrom_SQL.Value.Date);
            if (das.ClaimDateTo.HasValue)
                dasQ = dasQ.Where(o => o.ClaimDate.Date <= das.ClaimDateTo_SQL.Value.Date);
            
            #endregion

            #region Item specific filters

            if (das.ItemID > 0) dasQ = dasQ.Where(o => o.ItemID == das.ItemID);
            else if (!string.IsNullOrEmpty(das.ItemNo))
                dasQ = dasQ.Where(o => SqlMethods.Like(o.ItemNo.ToLower(), das.ItemNo.ToLower()));

            if (!string.IsNullOrEmpty(das.DOT))
                dasQ = dasQ.Where(o => SqlMethods.Like(o.DOT.ToLower(), das.DOT.ToLower()));
            
            if (!string.IsNullOrEmpty(das.Serial))
                dasQ = dasQ.Where(o => SqlMethods.Like(o.Serial.ToLower(), das.Serial.ToLower()));

            #endregion


            return dasQ;
        }
    }
}
