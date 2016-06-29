using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CPM.DAL;
using CPM.Services;
using CPM.Helper;

namespace CPM.Controllers
{
    //[CompressFilter] - DON'T
    [IsAuthorize(IsAuthorizeAttribute.Rights.NONE)]//Special case for some dirty session-abandoned pages and hacks
    public partial class DashboardController : BaseController
    {

        public ActionResult Charts(int? index, string qData)
        {
            //Special case: Set the filter back if it existed so that if the user "re-visits" the page he gets the previous filter (unless reset or logged off)
            searchOpts = new vw_Claim_Dashboard(); // _Session.Search[Filters.list.Dashboard];

            populateData(true);
            ViewData["gridPageSize"] = gridPageSize; // Required to adjust pagesize for grid

            // No need to return view - it'll fetched by ajax in partial rendering
            return View();
        }

        #region List Grid Excel

        //[CompressFilter] - DON'T
        public ActionResult ClaimDetailsReport()
        {
            //Special case: Set the filter back if it existed so that if the user "re-visits" the page he gets the previous filter (unless reset or logged off)
            searchOpts = new vw_ClaimWithItemDetail();  //_Session.Search[Filters.list.Dashboard];

            populateReportData(true, new vw_ClaimWithItemDetail());
            
            // No need to return view - it'll fetched by ajax in partial rendering
            return View(new List<vw_ClaimWithItemDetail>());
        }

        #region Will need POST
        
        [HttpPost]//Don't mention GET or post as this is required for both!
        [SkipModelValidation]
        public ActionResult ClaimDetailsReport(vw_ClaimWithItemDetail searchData, int? index, string qData, bool? fetchAll)
        {
            base.SetTempDataSort(ref index);// Set TempDate, Sort & index
            //Make sure searchOpts is assigned to set ViewState
            //vw_ClaimWithItemDetail oldSearchOpts = (vw_ClaimWithItemDetail)searchOpts;
            //searchOpts = new vw_ClaimWithItemDetail();// { Archived = oldSearchOpts.Archived };// CAUTION: otehrwise archived saved search will show null records
            populateReportData(true, searchData);

            index = (index > 0) ? index + 1 : index; // paging starts with 2
            searchOpts = searchData;

            var result = new DashboardService().ClaimWithDetails((vw_ClaimWithItemDetail)searchOpts, 1, gridPageSize);

            /*searchOpts = new vw_ClaimWithItemDetail();
            populateReportData(false, (vw_ClaimWithItemDetail)searchOpts);*/

            return View(result);
        }                
               
        #endregion

        [HttpPost]
        [SkipModelValidation]
        public ActionResult ClaimDetailsReportExcel()
        {
            //HttpContext context = ControllerContext.HttpContext.CurrentHandler;
            //Essense of : http://stephenwalther.com/blog/archive/2008/06/16/asp-net-mvc-tip-2-create-a-custom-action-result-that-returns-microsoft-excel-documents.aspx
            this.Response.Clear();
            this.Response.AddHeader("content-disposition", "attachment;filename=" + "ClaimWithDetails_" + _SessionUsr.ID + ".xls"); // NOT xlsx
            this.Response.Charset = "";
            this.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            this.Response.ContentType = "application/vnd.ms-excel";

            //DON'T do the following -             //this.Response.Write(content);            //this.Response.End();
            vw_ClaimWithItemDetail searchData = (vw_ClaimWithItemDetail)searchOpts;
            populateReportData(false, searchData);
            var result = new DashboardService().ClaimWithDetails((vw_ClaimWithItemDetail)searchData, 1, gridPageSize); // gridPageSize not effective for excel
                        
            //populateReportData(false, searchOpts);

            return View("ClaimWithDetails", result);
        }

        [HttpGet]
        public ActionResult ClaimDetailsReportExcel(string dummy)
        { // special case handling for sessiontimeout while loading excel download or user somehow trying to access the excel directly. SO : 16658020
            return RedirectToAction("ClaimWithDetails", "Dashboard");
        }
               
        /*[OutputCacheAttribute(VaryByParam = "*", Duration = 0, NoStore = true)] // disable caching SO : 12948156
        public ActionResult ClaimWithDetails()
        {
            //if (_Session.IsInternal && _SessionUsr.RoleID == (int)SecurityService.Roles.Admin)
            //{
            //    ViewData["Message"] = "You do not have access to Claim with details report.";
            //    return RedirectToAction("NoAccess", "Common");
            //}

            //HttpContext context = ControllerContext.HttpContext.CurrentHandler;
            //Essense of : http://stephenwalther.com/blog/archive/2008/06/16/asp-net-mvc-tip-2-create-a-custom-action-result-that-returns-microsoft-excel-documents.aspx
            this.Response.Clear();
            this.Response.AddHeader("content-disposition", "attachment;filename=" + "ClaimWithDetails.xls");
            this.Response.Charset = "";
            this.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            this.Response.ContentType = "application/vnd.ms-excel";

            //DON'T do the following
            //this.Response.Write(content);
            //this.Response.End();

            var result = new DashboardService().ClaimWithDetails();
            return View("ClaimWithDetails", result);
        }
        */
        #endregion

        public void populateReportData(bool fetchOtherData, vw_ClaimWithItemDetail searchOptions)
        {
            //using (MiniProfiler.Current.Step("Populate lookup Data"))
            {
                if (_Session.IsOnlyCustomer) searchOptions.CustID = _SessionUsr.OrgID;//Set the cust filter
                //if (_Session.IsOnlyVendor) searchOptions.VendorID = _SessionUsr.OrgID;//Set the Vendor filter
                if (_Session.IsOnlySales) searchOptions.SalespersonID = _SessionUsr.ID;//Set the Sales filter
                
                if (fetchOtherData)
                    ViewData["Status"] = new LookupService().GetLookup(LookupService.Source.Status);
            }
        }
    }
}
