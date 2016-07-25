using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CPM.DAL;
using CPM.Services;
using CPM.Helper;

namespace CPM.Controllers
{
    //[CompressFilter] - don't use it here
    [IsAuthorize(IsAuthorizeAttribute.Rights.NONE)]//Special case for some dirty session-abandoned pages and hacks
    public partial class ClaimController : BaseController
    {        
        #region Actions for Items (ClaimDetail) - Secure

        [AccessClaim("ClaimID")]
        [CacheControl(HttpCacheability.NoCache), HttpGet]
        public ActionResult Items(int ClaimID, string ClaimGUID, int? DetailID)
        {
            //ViewData["Brands"] = new LookupService().GetLookup(LookupService.Source.BrandItems);
            //ViewData["ClaimGUID"] = ClaimGUID;
            return View();
        }
                
        public ItemKOModel GetItemKOModel(int ClaimID, string ClaimGUID)
        {
           //Set Item object
            ClaimDetail newObj = new ClaimDetail() { ID = 0, _Added = true, ClaimID = ClaimID, LastModifiedBy = _SessionUsr.ID, LastModifiedDate = DateTime.Now, Archived = false, aDFilesJSON = "" };

            ItemKOModel vm = new ItemKOModel()
            {
                 ItemToAdd = newObj, EmptyItem = newObj,
                 AllItems = new ClaimDetailService().Search(ClaimID, null)
            };

            // Lookup data
            vm.Defects = new LookupService().GetLookup(LookupService.Source.Defect);
            
            vm.showGrid = true;
            return vm;
        }        
        
        #endregion

        // HT: Special temp action to show New Item entry (not fully functional - only for View and client side functionality)
        #region HT: DELETE if we're not going to keep New Item Entry

        [AccessClaim("ClaimID")]
        [CacheControl(HttpCacheability.NoCache), HttpGet]
        public ActionResult Manage2(int ClaimID, bool? printClaimAfterSave)
        {
            ViewData["oprSuccess"] = base.operationSuccess; //oprSuccess will be reset after this
            ViewData["printClaimAfterSave"] = (TempData["printClaimAfterSave"] ?? false);

            #region Add mode - add new and return it in editmode
            if (ClaimID <= Defaults.Integer)
            {// HT: CAREFUL: Add mode in which we need to add a new record
                // Also handles special case for customer to set default SP for him
                string spNameForCustomer = string.Empty;
                Claim NewClaim = new ClaimService().AddDefault(_SessionUsr.ID, _SessionUsr.OrgID, _Session.IsOnlyCustomer, ref spNameForCustomer);
                //Session.Claims[NewClaim.ClaimGUID] = NewClaim;
                //return RedirectToAction("Manage", new { ClaimID = NewClaim.ID, ClaimGUID = NewClaim.ClaimGUID });
                ClaimKOModel vmClaim = doAddEditPopulateKO(ClaimService.GetVWFromClaimObj(NewClaim, spNameForCustomer));
                return View(vmClaim);
            }
            #endregion

            #region Edit mode
            else
            {
                #region Get Claim view and check if its empty or archived - redirect

                vw_Claim_Master_User_Loc vw = new ClaimService().GetClaimById(ClaimID);

                if (vw.ID == Defaults.Integer && vw.StatusID == Defaults.Integer && vw.AssignedTo == Defaults.Integer)
                {
                    ViewData["Message"] = "Claim not found"; return View("DataNotFound"); /* deleted claim accessed from Log*/
                }
                // In case an archived entry is accessed
                if (vw.Archived)
                    return RedirectToAction("Archived", new { ClaimID = ClaimID });
                //Empty so invalid ClaimID - go to Home
                if (vw == new ClaimService().emptyView)
                    return RedirectToAction("List", "Dashboard");

                #endregion

                //Reset the Session Claim object
                Claim claimObj = ClaimService.GetClaimObjFromVW(vw);
                //_Session.Claim = claimObj;
                //_Session.Claims[claimObj.ClaimGUID] = claimObj;// Populate original obj

                ClaimKOModel vmClaim = doAddEditPopulateKO(vw);
                return View(vmClaim);
            }
            #endregion
        }

        [HttpPost]
        [AccessClaim("ClaimID")]
        public ActionResult Manage2(int ClaimID, bool isAddMode,
            [FromJson]vw_Claim_Master_User_Loc claimObj, [FromJson] IEnumerable<ClaimDetail> items,
            [FromJson] IEnumerable<Comment> comments, [FromJson] IEnumerable<FileHeader> files, bool? printClaimAfterSave)
        {
            bool success = false;
            //return new JsonResult() { Data = new{ msg = "success"}};

            //HT: Note the following won't work now as we insert a record in DB then get it back in edit mode for Async edit
            //bool isAddMode = (claimObj.ID <= Defaults.Integer); 

            #region Perform operation proceed and set result

            int result = new ClaimService().AsyncBulkAddEditDelKO(claimObj, claimObj.StatusIDold, items, comments, files);
            success = result > 0;

            if (!success) { /*return View(claimObj);*/}
            else //Log Activity based on mode
            {
                claimObj.ClaimNo = result;// Set Claim #
                ActivityLogService.Activity act = isAddMode ? ActivityLogService.Activity.ClaimAdd : ActivityLogService.Activity.ClaimEdit;
                new ActivityLogService(act).Add(new ActivityHistory() { ClaimID = result, ClaimText = claimObj.ClaimNo.ToString() });
            }

            #endregion

            base.operationSuccess = success;//Set opeaon success
            _Session.ClaimsInMemory.Remove(claimObj.ClaimGUID); // Remove the Claim from session
            //_Session.ResetClaimInSessionAndEmptyTempUpload(claimObj.ClaimGUID); // reset because going back to Manage will automatically create new GUID

            if (success)
                TempData["printClaimAfterSave"] = printClaimAfterSave.HasValue && printClaimAfterSave.Value;

            return RedirectToAction("Manage2", new { ClaimID = result });
        }

        #endregion
    }
}

namespace CPM.DAL
{
    public class ItemKOModel
    {
        public ClaimDetail EmptyItem { get; set; }
        public ClaimDetail ItemToAdd { get; set; }
        public List<ClaimDetail> AllItems { get; set; }
        public IEnumerable Defects { get; set; }
        public bool showGrid { get; set; }
    }
}

/* HT: IMP for future usage f Grouped Dropdown
         public IEnumerable<GroupDropListItem> GetGroupedDefectList(System.Collections.IEnumerable defectsColl)
        {// DON'T forget to SORT by Category !!!
            List<MasterDefect> defects = defectsColl.Cast<MasterDefect>().ToList();
            List<GroupDropListItem> items = new List<GroupDropListItem>();

            string oldCategory = " ";//To avoid empty == empty
            foreach (MasterDefect defect in defects)
            {
                if (oldCategory != (defect.Category??"").Trim())
                {//Process parent
                    GroupDropListItem parent = new GroupDropListItem();
                    parent.Name = defect.Category;
                    parent.Items = new List<OptionItem>();//DON'T forget to initialize or it'll return exception while adding
                    //fetch and add child records
                    List<MasterDefect> children = (from d in defects where d.Category == defect.Category 
                                                   orderby d.SortOrder select d).ToList();
                    foreach (MasterDefect child in children)
                        parent.Items.Add(new OptionItem() { Text = child.Title, Value = child.ID.ToString() });
                    //finally add parent to the items list
                    items.Add(parent);
                    //set existing category to skip other records
                    oldCategory = defect.Category;
                }
            }

            return items;
        }
        */
