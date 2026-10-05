using System.Net;
using SSLConfiguration.Infrastructure;
using SSLConfiguration.Infrastructure.DataAccess;
using SSLConfiguration.Infrastructure.Persistence;
using VerisignGateway;
using VerisignGateway.GS_MarkService;

namespace SSLConfiguration.Application
{
    /// <summary>
    /// Thin layer architecture / Core migration: keep same BLGlobalsign method names as old repo.
    /// IssueCertificate uses GetGlobalSignOrderDetail.
    /// </summary>
    public class BLGlobalsign
    {
        /// <summary>
        /// Same method as old BLGlobalsign.GetGlobalSignOrderDetail.
        /// Loads DB details for IssueCertificate; CA status calls use VerisignGateway.VerisignAPIHelper (same DLL).
        /// Child-cert / VMC logo MapPath helpers deferred (System.Web) — not required for IssueCertificate next-step routing.
        /// </summary>
        public static GlobalSignOrderDetailResponse GetGlobalSignOrderDetail(GlobalSignOrderDetailRequest globalSignOrderDetailRequest)
        {
            GlobalSignOrderDetailResponse globalSignOrderDetailResponse = new GlobalSignOrderDetailResponse();
            globalSignOrderDetailResponse.GSOrderDetail = new GSOrderDetailInfo();

            try
            {
                // certificate details
                StoreOrder? storeOrderDetail = StoreOrderDataAccess.GetByPin(globalSignOrderDetailRequest.Pin ?? string.Empty);

                if (storeOrderDetail == null)
                {
                    globalSignOrderDetailResponse.StatusCode = -1;
                    globalSignOrderDetailResponse.ErrorDetail.ErrorMessage = "No order details found";

                    return globalSignOrderDetailResponse;
                }

                CACredential? objCACredential = BLGeneral.GetCACredentials(storeOrderDetail.StoreOrderId);

                globalSignOrderDetailResponse.StoreOrderId = storeOrderDetail.StoreOrderId;
                globalSignOrderDetailResponse.CAContactName = objCACredential?.ContactName;
                globalSignOrderDetailResponse.CAContactEmail = objCACredential?.ContactEmail;
                globalSignOrderDetailResponse.CredentialCode = objCACredential?.CredentialCode;
                globalSignOrderDetailResponse.IsAllowIssueCertificate = storeOrderDetail.IsAllowIssueCertificate;
                if (storeOrderDetail.IsUsed == false && storeOrderDetail.IsCancel == false)
                {
                    globalSignOrderDetailResponse.OrderStatus = APIConstant.ORDERSTATUS_LINKPENDING;
                    return globalSignOrderDetailResponse;
                }
                else if (storeOrderDetail.IsUsed == false && storeOrderDetail.IsCancel == true)
                {
                    globalSignOrderDetailResponse.OrderStatus = APIConstant.ORDERSTATUS_CANCELLED;
                    return globalSignOrderDetailResponse;
                }

                // globalsign order details
                GlobalSignOrderDetail? globalSignOrderDetail = GlobalSignOrderDetailDataAccess.GetByStoreOrderId(storeOrderDetail.StoreOrderId);

                globalSignOrderDetailResponse.GlobalSignOrderDetail = globalSignOrderDetail;

                if (globalSignOrderDetail == null)
                {
                    return globalSignOrderDetailResponse;
                }

                // csr detail
                CSRDetail? csrDetail = CSRDetailDataAccess.GetById(globalSignOrderDetail.CSRDetailId);
                globalSignOrderDetailResponse.EntityCSRDetail = csrDetail;

                // organisation details
                GlobalSignOrganizationInfo? globalSignOrgInfo = GlobalSignOrganizationInfoDataAccess.GetById(globalSignOrderDetail.GlobalSignOrganizationInfoID);
                globalSignOrderDetailResponse.OrganizationDetails = globalSignOrgInfo;

                // contact details
                GlobalSignContactInfo? globalSignContactInfo = GlobalSignContactInfoDataAccess.GetById(globalSignOrderDetail.ContactInfoId);
                globalSignOrderDetailResponse.ContactDetails = globalSignContactInfo;

                // requestor details
                GlobalSignContactInfo? requestorInfo = GlobalSignContactInfoDataAccess.GetById(globalSignOrderDetail.RequestorInfoId);
                globalSignOrderDetailResponse.RequestorDetails = requestorInfo;

                // approver details
                GlobalSignContactInfo? approverInfo = GlobalSignContactInfoDataAccess.GetById(globalSignOrderDetail.ApprovalInfoId);
                globalSignOrderDetailResponse.ApprovalDetails = approverInfo;

                // authorize details
                GlobalSignContactInfo? authorizeInfo = GlobalSignContactInfoDataAccess.GetById(globalSignOrderDetail.AuthorizedInfoId);
                globalSignOrderDetailResponse.AuthorizedDetails = authorizeInfo;

                // get additional domains
                List<AdditionalDomain> additionalDomains = AdditionalDomainDataAccess.GetByStoreOrderId(storeOrderDetail.StoreOrderId);

                if (additionalDomains.Count > 0)
                {
                    globalSignOrderDetailResponse.AdditionalDomainList = additionalDomains;
                }
                else
                    globalSignOrderDetailResponse.AdditionalDomainList = new List<AdditionalDomain>();

                if (!string.IsNullOrEmpty(storeOrderDetail.ApiOrderNo) && objCACredential != null)
                {
                    // get code sign order detail — same VerisignAPIHelper DLL calls as old repo
                    if (storeOrderDetail.ProductId == (int)VerisignGateway.ProductCode.GSCodeSign || storeOrderDetail.ProductId == (int)VerisignGateway.ProductCode.GSEVCodeSign)
                    {
                        GSCodeSignOrderDetailInfo objGSDetail = VerisignAPIHelper.GetGSCodeSignOrderStatus(storeOrderDetail.ApiOrderNo, objCACredential.GetGlobalSignCACredential());

                        if (objGSDetail.error != null && objGSDetail.error.ErrorCode < 0)
                        {
                            Exception ex = new Exception(objGSDetail.error.ErrorMessage);
                            LogWriter.LogErrorDetails(ex);
                        }
                        else
                        {
                            globalSignOrderDetailResponse.GSCodeSignOrderDetail = new GSCodeSignOrderDetailInfo();
                            globalSignOrderDetailResponse.GSCodeSignOrderDetail.ApproverEmail = objGSDetail.Subscriber.Email;
                            globalSignOrderDetailResponse.GSCodeSignOrderDetail.DomainName = objGSDetail.DomainName;
                            globalSignOrderDetailResponse.GSCodeSignOrderDetail.EndDate = objGSDetail.EndDate;
                            globalSignOrderDetailResponse.GSCodeSignOrderDetail.GlobalSignOrderID = objGSDetail.GlobalSignOrderID;
                            globalSignOrderDetailResponse.GSCodeSignOrderDetail.OrderStatus = objGSDetail.OrderStatus;
                            globalSignOrderDetailResponse.OrderStatus = objGSDetail.OrderStatus;
                            globalSignOrderDetailResponse.GSCodeSignOrderDetail.StartDate = objGSDetail.StartDate;
                            globalSignOrderDetailResponse.GSCodeSignOrderDetail.ValidityPeriod = objGSDetail.ValidityPeriod;
                            globalSignOrderDetailResponse.GSCodeSignOrderDetail.Subscriber = objGSDetail.Subscriber;

                            if (storeOrderDetail.ProductId == (int)VerisignGateway.ProductCode.GSEVCodeSign)
                            {
                                if (objGSDetail.ApproverInfo != null)
                                    globalSignOrderDetailResponse.GSCodeSignOrderDetail.ApproverInfo = objGSDetail.ApproverInfo;

                                if (objGSDetail.RequestorInfo != null)
                                    globalSignOrderDetailResponse.GSCodeSignOrderDetail.RequestorInfo = objGSDetail.RequestorInfo;

                                if (objGSDetail.AuthorizedInfo != null)
                                    globalSignOrderDetailResponse.GSCodeSignOrderDetail.AuthorizedInfo = objGSDetail.AuthorizedInfo;

                                if (objGSDetail.OrganisationInfo != null)
                                    globalSignOrderDetailResponse.GSCodeSignOrderDetail.OrganisationInfo = objGSDetail.OrganisationInfo;
                            }

                            GSGetSANValidationStatusResponse objGSResponse = VerisignAPIHelper.GSGetSANValidationStatus(globalSignOrderDetailResponse.GSCodeSignOrderDetail.GlobalSignOrderID, objCACredential.GetGlobalSignCACredential());

                            if (objGSResponse.error.ErrorCode == 0)
                                globalSignOrderDetailResponse.DomainVerificationPage = objGSResponse.DomainVerificationPage;

                            return globalSignOrderDetailResponse;
                        }
                    }
                    else if (!(storeOrderDetail.ApiOrderNo.Contains("_") &&
                            (storeOrderDetail.ProductId == (int)VerisignGateway.ProductCode.PrimeSSLVerifiedMarkCertificate ||
                             storeOrderDetail.ProductId == (int)VerisignGateway.ProductCode.PrimeSSLCommonMarkCertificate)))
                    {
                        GSOrderDetailInfo objGSDetail = VerisignAPIHelper.GetGSOrderStatus(storeOrderDetail.ApiOrderNo, objCACredential.GetGlobalSignCACredential());

                        if (objGSDetail.error != null && objGSDetail.error.ErrorCode < 0)
                        {
                            Exception ex = new Exception(objGSDetail.error.ErrorMessage);
                            LogWriter.LogErrorDetails(ex);
                        }
                        else
                        {
                            globalSignOrderDetailResponse.GSOrderDetail.ApproverEmail = objGSDetail.ApproverEmail;
                            globalSignOrderDetailResponse.GSOrderDetail.DomainName = objGSDetail.DomainName;
                            globalSignOrderDetailResponse.GSOrderDetail.EndDate = objGSDetail.EndDate;
                            globalSignOrderDetailResponse.GSOrderDetail.GlobalSignOrderID = objGSDetail.GlobalSignOrderID;
                            globalSignOrderDetailResponse.GSOrderDetail.OrderStatus = objGSDetail.OrderStatus;
                            globalSignOrderDetailResponse.OrderStatus = objGSDetail.OrderStatus;
                            globalSignOrderDetailResponse.GSOrderDetail.StartDate = objGSDetail.StartDate;
                            globalSignOrderDetailResponse.GSOrderDetail.ValidityPeriod = objGSDetail.ValidityPeriod;

                            globalSignOrderDetailResponse.CAOrderValidFrom = objGSDetail.StartDate;
                            globalSignOrderDetailResponse.CAOrderValidTo = objGSDetail.EndDate;
                            globalSignOrderDetailResponse.CAOrderValidity = storeOrderDetail.Year;

                            if (storeOrderDetail.IsStoreMYP == true)
                            {
                                if (objGSDetail.StartDate != DateTime.MinValue)
                                {
                                    globalSignOrderDetailResponse.StoreOrderValidFrom = objGSDetail.StartDate;
                                }
                                else
                                {
                                    globalSignOrderDetailResponse.StoreOrderValidFrom = storeOrderDetail.StoreOrderValidFrom;
                                }
                                globalSignOrderDetailResponse.StoreOrderValidTo = storeOrderDetail.StoreOrderValidTo;
                            }
                        }

                        if (!string.IsNullOrEmpty(globalSignOrderDetailResponse.GSOrderDetail.GlobalSignOrderID))
                        {
                            GSGetSANValidationStatusResponse objResponse = VerisignAPIHelper.GSGetSANValidationStatus(globalSignOrderDetailResponse.GSOrderDetail.GlobalSignOrderID, objCACredential.GetGlobalSignCACredential());

                            if (objResponse.error.ErrorCode == 0)
                                globalSignOrderDetailResponse.DomainVerificationPage = objResponse.DomainVerificationPage;
                        }
                    }
                }
                else
                {
                    globalSignOrderDetailResponse.GSOrderDetail.OrderStatus = APIConstant.ORDERSTATUS_LINKPENDING;
                    globalSignOrderDetailResponse.OrderStatus = APIConstant.ORDERSTATUS_LINKPENDING;
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                throw;
            }
            return globalSignOrderDetailResponse;
        }

        #region Place Order

        /// <summary>
        /// Same as old BLGlobalsign.GetGSOrderRequest.
        /// </summary>
        public static GSOrderRequest GetGSOrderRequest(PF_Request objPFRequest)
        {
            GSOrderRequest objReturn = new GSOrderRequest();

            objReturn.ApproverEmail = objPFRequest.GlobalSignOrderRequest.ApprovalEmail;

            objReturn.CSR = objPFRequest.CSR;

            if (objPFRequest.CSRDetailRow != null)
                objReturn.DomainName = objPFRequest.CSRDetailRow.DomainName;

            objReturn.isNew = objPFRequest.isRenew ? false : true;

            objReturn.NoOfServer = 1;

            if (!string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OldGSOrderID) && objPFRequest.isRenew)
                objReturn.OldGSOrderID = objPFRequest.GlobalSignOrderRequest.OldGSOrderID;

            objReturn.Years = objPFRequest.StoreOrderDetail!.Year;

            objReturn.PartnerOrderID = objPFRequest.StoreOrderDetail.StoreOrderId.ToString();
            objReturn.ProductCode = objPFRequest.StoreOrderDetail.ProductId;

            #region GlobalSign ContactInfo

            GlobalSignContactInfo? objTempContactInfo = null;

            if (objPFRequest.GlobalSignOrderRequest.ContactInfoRow != null)
            {
                objTempContactInfo = objPFRequest.GlobalSignOrderRequest.ContactInfoRow;
                objReturn.ContactInfo = new ContactDetail();

                objReturn.ContactInfo.Email = objTempContactInfo.Email;
                objReturn.ContactInfo.FirstName = objTempContactInfo.FirstName;
                objReturn.ContactInfo.FunctionInOrg = objTempContactInfo.FunctionInOrg;
                objReturn.ContactInfo.LastName = objTempContactInfo.LastName;
                objReturn.ContactInfo.OrganizationName = objTempContactInfo.OrganizationName;
                objReturn.ContactInfo.OrganizationUnit = objTempContactInfo.OrgUnit;
                objReturn.ContactInfo.Phone = objTempContactInfo.PhoneNo;
            }

            #endregion

            // set CA Credential details
            //CACredential caCredential = BLGeneral.GetCACredential(BLGeneral.GetCACredentialCodeForOrder(objPFRequest.StoreOrderDetail.StoreOrderId));
            //objReturn.GSCredential.UserName = caCredential.UserName;
            //objReturn.GSCredential.Password = caCredential.Password;
            CACredential? objCACredential = BLGeneral.GetCACredentials(objPFRequest.StoreOrderDetail.StoreOrderId);

            objReturn.GSCredential = objCACredential!.GetGlobalSignCACredential();

            return objReturn;
        }

        /// <summary>
        /// Same as old BLGlobalsign.GetGSCodeSignOrderRequest.
        /// </summary>
        public static GSCodeSignOrderRequest GetGSCodeSignOrderRequest(PF_Request objPFRequest)
        {
            GSCodeSignOrderRequest objReturn = new GSCodeSignOrderRequest();
            objReturn.Year = objPFRequest.StoreOrderDetail!.Year;

            objReturn.IsNew = objPFRequest.isRenew ? false : true;
            if (!string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OldGSOrderID) && objPFRequest.isRenew)
                objReturn.OldGSOrderId = objPFRequest.GlobalSignOrderRequest.OldGSOrderID;

            objReturn.PickupPassword = objPFRequest.GlobalSignOrderRequest.PickupPassword;

            // csr detail
            if (objPFRequest.CSRDetailRow != null)
            {
                objReturn.DomainNameAttributes = new GSDomainNameAttributes()
                {
                    CommonName = objPFRequest.CSRDetailRow.DomainName,
                    City = objPFRequest.CSRDetailRow.Locality,
                    Country = objPFRequest.CSRDetailRow.Country,
                    Email = objPFRequest.CSRDetailRow.Email,
                    OrganizationUnit = objPFRequest.CSRDetailRow.OrganisationUnit,
                    State = objPFRequest.CSRDetailRow.State,
                };
            }

            // Subscriber details
            if (objPFRequest.GlobalSignOrderRequest.ContactInfoRow != null)
            {
                objReturn.Subscriber = new GSCodeSignContact()
                {
                    Address1 = objPFRequest.GlobalSignOrderRequest.ContactInfoRow.Address1,
                    Address2 = objPFRequest.GlobalSignOrderRequest.ContactInfoRow.Address2,
                    City = objPFRequest.GlobalSignOrderRequest.ContactInfoRow.City,
                    Country = objPFRequest.GlobalSignOrderRequest.ContactInfoRow.Country,
                    Department = objPFRequest.GlobalSignOrderRequest.ContactInfoRow.OrgUnit,
                    Email = objPFRequest.GlobalSignOrderRequest.ContactInfoRow.Email,
                    FirstName = objPFRequest.GlobalSignOrderRequest.ContactInfoRow.FirstName,
                    LastName = objPFRequest.GlobalSignOrderRequest.ContactInfoRow.LastName,
                    JobTitle = string.Empty,
                    OrganizatonPhone = objPFRequest.GlobalSignOrderRequest.ContactInfoRow.PhoneNo,
                    OrganizationName = objPFRequest.GlobalSignOrderRequest.ContactInfoRow.OrganizationName,
                    OrganizationUnit = objPFRequest.GlobalSignOrderRequest.ContactInfoRow.OrgUnit,
                    PostalCode = objPFRequest.GlobalSignOrderRequest.ContactInfoRow.ZipCode,
                    State = objPFRequest.GlobalSignOrderRequest.ContactInfoRow.State,
                };
            }

            // Organization details
            if (objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow != null)
            {
                objReturn.OrganizationInfo = new GSCodeSignOrganizationInfo()
                {
                    Address1 = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Address1,
                    Address2 = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Address2,
                    City = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.City,
                    Country = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Country,
                    BusinessAssumedName = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.LegalName,
                    BusinessCategory = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Division,
                    PostalCode = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.ZipCode,
                    State = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.State,
                    Phone = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.PhoneNo,
                    Fax = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Fax
                };
                objReturn.OrganizationInfo.JuridictionInfo = new GSCodeSignJuridictionInfo()
                {
                    City = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionCity,
                    State = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionState,
                    Country = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionCountry,
                    InCorpRegNumber = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionRegNo
                };
            }

            if (objPFRequest.GlobalSignOrderRequest.ApproverInfoRow != null && !string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.ApproverInfoRow.FirstName))
            {
                objReturn.OrganizationInfo!.ApproverInfo = new GSCodeSignContact()
                {
                    Email = objPFRequest.GlobalSignOrderRequest.ApproverInfoRow.Email,
                    OrganizationName = objPFRequest.GlobalSignOrderRequest.ApproverInfoRow.OrganizationName,
                    FirstName = objPFRequest.GlobalSignOrderRequest.ApproverInfoRow.FirstName,
                    LastName = objPFRequest.GlobalSignOrderRequest.ApproverInfoRow.LastName,
                    OrganizatonPhone = objPFRequest.GlobalSignOrderRequest.ApproverInfoRow.PhoneNo,
                    JobTitle = objPFRequest.GlobalSignOrderRequest.ApproverInfoRow.Title,
                };
            }

            if (objPFRequest.GlobalSignOrderRequest.RequestorInfoRow != null && !string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.RequestorInfoRow.FirstName))
            {
                objReturn.OrganizationInfo!.RequestorInfo = new GSCodeSignContact()
                {
                    Email = objPFRequest.GlobalSignOrderRequest.RequestorInfoRow.Email,
                    OrganizationName = objPFRequest.GlobalSignOrderRequest.RequestorInfoRow.OrganizationName,
                    FirstName = objPFRequest.GlobalSignOrderRequest.RequestorInfoRow.FirstName,
                    LastName = objPFRequest.GlobalSignOrderRequest.RequestorInfoRow.LastName,
                    OrganizatonPhone = objPFRequest.GlobalSignOrderRequest.RequestorInfoRow.PhoneNo,
                    JobTitle = objPFRequest.GlobalSignOrderRequest.RequestorInfoRow.Title,
                };
            }

            if (objPFRequest.GlobalSignOrderRequest.AuthorisedInfoRow != null && !string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.AuthorisedInfoRow.FirstName))
            {
                objReturn.OrganizationInfo!.AuthorizedSignerInfo = new GSCodeSignContact()
                {
                    Email = objPFRequest.GlobalSignOrderRequest.AuthorisedInfoRow.Email,
                    OrganizationName = objPFRequest.GlobalSignOrderRequest.AuthorisedInfoRow.OrganizationName,
                    FirstName = objPFRequest.GlobalSignOrderRequest.AuthorisedInfoRow.FirstName,
                    LastName = objPFRequest.GlobalSignOrderRequest.AuthorisedInfoRow.LastName,
                    OrganizatonPhone = objPFRequest.GlobalSignOrderRequest.AuthorisedInfoRow.PhoneNo,
                    JobTitle = objPFRequest.GlobalSignOrderRequest.AuthorisedInfoRow.Title,
                };
            }

            // credential
            CACredential? objCACredential = BLGeneral.GetCACredentials(objPFRequest.StoreOrderDetail.StoreOrderId);
            objReturn.GSCredential = objCACredential!.GetGlobalSignCACredential();

            return objReturn;
        }

        /// <summary>
        /// Same as old BLSymantec.SetErrorFromSymantec used by GS PlaceOrder factories.
        /// </summary>
        public static void SetErrorFromGlobalSign(PF_Response objPFResponse, OrderResponse objGSOrderResponse)
        {
            objPFResponse.ErrorCode = objGSOrderResponse.error.ErrorCode;
            objPFResponse.ErrorFiled = objGSOrderResponse.error.ErrorField;
            objPFResponse.ErrorMessage = objGSOrderResponse.error.ErrorMessage;
        }

        /// <summary>
        /// Same as old BLGlobalsign.GetGlobalSignOrderRequestObject_VMCCMC (logo read from ContentRoot/Uploads/VMC).
        /// </summary>
        public static QbV1MarkOrderRequest GetGlobalSignOrderRequestObject_VMCCMC(PF_Request objPFRequest, string orderType)
        {
            QbV1MarkOrderRequest objGSRequest = new QbV1MarkOrderRequest();

            #region AUTH
            objGSRequest.OrderRequestHeader = new OrderRequestHeader
            {
                AuthToken = new AuthToken
                {
                    UserName = objPFRequest.CACredentialDetails!.UserName,
                    Password = objPFRequest.CACredentialDetails.Password
                }
            };
            #endregion

            #region ORDER
            objGSRequest.OrderRequestParameter = new MarkOrderRequestParameter
            {
                ProductCode = orderType,
                OrderKind = "new",
                Licenses = "1",
                ValidityPeriod = new ValidityPeriod { Months = (objPFRequest.StoreOrderDetail!.Year * 12).ToString() }
            };
            #endregion

            #region ORGANIZATION EV
            objGSRequest.OrganizationInfoEV = new OrganizationInfoEVNative
            {
                BusinessCategoryCode = objPFRequest.GlobalSignOrderRequest!.OrganisationInfoRow!.BusinessCategory,
                OrganizationAddress = new OrganizationAddressInfoNative
                {
                    AddressLine1 = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Address1,
                    City = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.City,
                    Region = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.State,
                    PostalCode = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.ZipCode,
                    Country = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Country,
                    Phone = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.PhoneNo
                }
            };
            #endregion

            #region REQUESTOR
            objGSRequest.RequestorInfo = new RequestorApproverInfoNative
            {
                FirstName = objPFRequest.GlobalSignOrderRequest.RequestorInfoRow!.FirstName,
                LastName = objPFRequest.GlobalSignOrderRequest.RequestorInfoRow.LastName,
                Function = objPFRequest.GlobalSignOrderRequest.RequestorInfoRow.Title,
                OrganizationName = objPFRequest.GlobalSignOrderRequest.RequestorInfoRow.OrganizationName,
                OrganizationUnitNative = objPFRequest.GlobalSignOrderRequest.RequestorInfoRow.OrgUnit,
                Phone = objPFRequest.GlobalSignOrderRequest.RequestorInfoRow.PhoneNo,
                Email = objPFRequest.GlobalSignOrderRequest.RequestorInfoRow.Email
            };
            #endregion

            #region APPROVER
            objGSRequest.ApproverInfo = new RequestorApproverInfoNative
            {
                FirstName = objPFRequest.GlobalSignOrderRequest.ApproverInfoRow!.FirstName,
                LastName = objPFRequest.GlobalSignOrderRequest.ApproverInfoRow.LastName,
                Function = objPFRequest.GlobalSignOrderRequest.ApproverInfoRow.Title,
                OrganizationName = objPFRequest.GlobalSignOrderRequest.ApproverInfoRow.OrganizationName,
                OrganizationUnitNative = objPFRequest.GlobalSignOrderRequest.ApproverInfoRow.OrgUnit,
                Phone = objPFRequest.GlobalSignOrderRequest.ApproverInfoRow.PhoneNo,
                Email = objPFRequest.GlobalSignOrderRequest.ApproverInfoRow.Email
            };
            #endregion

            #region AUTHORIZED SIGNER
            objGSRequest.AuthorizedSignerInfo = new AuthorizedSignerInfoNative
            {
                OrganizationName = objPFRequest.GlobalSignOrderRequest.AuthorisedInfoRow!.OrganizationName,
                FirstName = objPFRequest.GlobalSignOrderRequest.AuthorisedInfoRow.FirstName,
                LastName = objPFRequest.GlobalSignOrderRequest.AuthorisedInfoRow.LastName,
                Function = objPFRequest.GlobalSignOrderRequest.AuthorisedInfoRow.Title,
                Phone = objPFRequest.GlobalSignOrderRequest.AuthorisedInfoRow.PhoneNo,
                Email = objPFRequest.GlobalSignOrderRequest.AuthorisedInfoRow.Email
            };
            #endregion

            #region JURISDICTION
            objGSRequest.JurisdictionInfo = new JurisdictionInfo
            {
                JurisdictionCountry = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionCountry,
                JurisdictionState = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionState,
                IncorporationAgencyRegistrationNumber = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.CorporateRegistrationNumber
            };
            #endregion

            #region CONTACT
            objGSRequest.ContactInfo = new ContactInfoNative
            {
                FirstName = objPFRequest.GlobalSignOrderRequest.ContactInfoRow!.FirstName,
                LastName = objPFRequest.GlobalSignOrderRequest.ContactInfoRow.LastName,
                Phone = objPFRequest.GlobalSignOrderRequest.ContactInfoRow.PhoneNo,
                Email = objPFRequest.GlobalSignOrderRequest.ContactInfoRow.Email
            };
            #endregion

            #region MARK INFO (VMC CORE)
            string ext = string.IsNullOrWhiteSpace(objPFRequest.GlobalSignOrderRequest.VMCCertificateDetail?.FileName)
                ? ".svg"
                : Path.GetExtension(objPFRequest.GlobalSignOrderRequest.VMCCertificateDetail.FileName);
            if (string.IsNullOrWhiteSpace(ext))
                ext = ".svg";

            string fileName = Convert.ToString(objPFRequest.StoreOrderDetail.SSLApiLinkId) + ext;
            string logoPath = Path.Combine(LogWriter.ContentRoot, "Uploads", "VMC", fileName);
            string base64Logo = Convert.ToBase64String(File.ReadAllBytes(logoPath));

            objGSRequest.MarkInfo = new MarkInfo
            {
                SvgLogoBase64 = base64Logo,
                TrademarkIdentifier = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.TrademarkIdentifier,
                TrademarkCountryOrRegionName = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.TrademarkCountryOrRegionName,
                TrademarkOfficeName = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.TrademarkOfficeName,
                DnAttributes = new DnAttributes
                {
                    CommonNameSAN = objPFRequest.CSRDetailRow != null
                        ? objPFRequest.CSRDetailRow.DomainName
                        : objPFRequest.GlobalSignOrderRequest.CSRDetailInfoRow!.DomainName,
                    Organization = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.LegalName,
                    Locality = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.City,
                    StateOrProvince = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.State,
                    Country = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Country
                }
            };
            #endregion

            #region SAN Additional
            List<SANEntry> sanEntries = new List<SANEntry>();
            var OptionValue = "true";

            if (objPFRequest.GlobalSignOrderRequest.AdditionalDomainsList != null &&
                objPFRequest.GlobalSignOrderRequest.AdditionalDomainsList.Count > 0)
            {
                string primaryDomain = objGSRequest.MarkInfo.DnAttributes.CommonNameSAN!.Trim().ToLower();
                foreach (var san in objPFRequest.GlobalSignOrderRequest.AdditionalDomainsList)
                {
                    if (!string.IsNullOrWhiteSpace(san.Key) && san.Key.ToLower() != primaryDomain)
                    {
                        sanEntries.Add(new SANEntry
                        {
                            SANOptionType = "7",
                            SubjectAltName = san.Key
                        });
                    }
                }

                if (sanEntries.Count > 0)
                    objGSRequest.SANEntries = sanEntries.ToArray();
                else
                    OptionValue = "false";
            }

            if (objPFRequest.GlobalSignOrderRequest.AdditionalDomainsList != null &&
                objPFRequest.GlobalSignOrderRequest.AdditionalDomainsList.Count > 0)
            {
                objGSRequest.OrderRequestParameter.Options = new Option[]
                {
                    new Option
                    {
                        OptionName = "SAN",
                        OptionValue = OptionValue
                    }
                };
            }
            #endregion

            return objGSRequest;
        }

        /// <summary>
        /// Same as old BLGlobalsign.SaveGlobslsignVMCCMCOrderInDB (EF Core transaction).
        /// </summary>
        public static void SaveGlobslsignVMCCMCOrderInDB(PF_Request objPFRequest, OrderResponse objGSOrderResponse, string? dvc)
        {
            BLStoreOrder.UpdateAPIOrderNoInStoreOrder(
                objPFRequest.StoreOrderDetail!.StoreOrderId,
                objGSOrderResponse.OrderNumber,
                objPFRequest.LanguageCode);

            using var dbContext = new SSLConfigurationEntities();
            using var transaction = dbContext.Database.BeginTransaction();
            try
            {
                CSRDetail objcsrdetail = objPFRequest.CSRDetailRow != null
                    ? CloneCsr(objPFRequest.CSRDetailRow)
                    : CloneCsr(objPFRequest.GlobalSignOrderRequest!.CSRDetailInfoRow!);

                dbContext.CSRDetails.Add(objcsrdetail);
                dbContext.SaveChanges();

                int SCSRinfo = objcsrdetail.CSRDetailId;
                int ReqInfoID = 0;
                int AppInfoID = 0;
                int AuInfoID = 0;
                int ContactInfoID = 0;
                int OrginfoID = 0;

                if (objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow != null
                    && !string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.LegalName)
                    && !string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Address1))
                {
                    if (objPFRequest.GlobalSignOrderRequest.RequestorInfoRow != null
                        && !string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.RequestorInfoRow.FirstName))
                    {
                        GlobalSignContactInfo objRequestorInfo = CloneContact(objPFRequest.GlobalSignOrderRequest.RequestorInfoRow);
                        dbContext.GlobalSignContactInfos.Add(objRequestorInfo);
                        dbContext.SaveChanges();
                        ReqInfoID = objRequestorInfo.GlobalSignContactInfoID;
                    }

                    if (objPFRequest.GlobalSignOrderRequest.ApproverInfoRow != null
                        && !string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.ApproverInfoRow.FirstName))
                    {
                        GlobalSignContactInfo objApproverInfo = CloneContact(objPFRequest.GlobalSignOrderRequest.ApproverInfoRow);
                        dbContext.GlobalSignContactInfos.Add(objApproverInfo);
                        dbContext.SaveChanges();
                        AppInfoID = objApproverInfo.GlobalSignContactInfoID;
                    }

                    if (objPFRequest.GlobalSignOrderRequest.ContactInfoRow != null
                        && !string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.ContactInfoRow.FirstName))
                    {
                        GlobalSignContactInfo objContactInfo = CloneContact(objPFRequest.GlobalSignOrderRequest.ContactInfoRow);
                        dbContext.GlobalSignContactInfos.Add(objContactInfo);
                        dbContext.SaveChanges();
                        ContactInfoID = objContactInfo.GlobalSignContactInfoID;
                    }

                    GlobalSignOrganizationInfo objgsoinfo = new GlobalSignOrganizationInfo
                    {
                        StoreOrderId = objPFRequest.StoreOrderDetail.StoreOrderId,
                        LegalName = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.LegalName) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.LegalName,
                        AssumedName = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.AssumedName) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.AssumedName,
                        Address1 = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Address1) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Address1,
                        Address2 = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Address2) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Address2,
                        City = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.City) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.City,
                        Country = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Country) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Country,
                        Fax = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Fax) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Fax,
                        PhoneNo = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.PhoneNo) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.PhoneNo,
                        ZipCode = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.ZipCode) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.ZipCode,
                        State = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.State) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.State,
                        Email = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Email) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Email,
                        Duns = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Duns) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Duns,
                        Division = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Division) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Division,
                        JurictionCity = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionCity) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionCity,
                        JurictionCountry = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionCountry) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionCountry,
                        JurictionRegNo = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionRegNo) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionRegNo,
                        JurictionState = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionState) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionState,
                        FirstName = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.FirstName) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.FirstName,
                        LastName = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.LastName) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.LastName,
                        RegisteredMarkLicenseExpiryDate = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.RegisteredMarkLicenseExpiryDate,
                        TrademarkIdentifier = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.TrademarkIdentifier,
                        TrademarkCountryOrRegionName = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.TrademarkCountryOrRegionName,
                        TrademarkOfficeName = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.TrademarkOfficeName,
                        BusinessCategory = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.BusinessCategory,
                        CorporateRegistrationNumber = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.CorporateRegistrationNumber) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.CorporateRegistrationNumber,
                        TrademarkURL = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.TrademarkURL
                    };

                    dbContext.GlobalSignOrganizationInfos.Add(objgsoinfo);
                    dbContext.SaveChanges();
                    OrginfoID = objgsoinfo.GlobalSignOrganizationInfoID;
                }
                else
                {
                    GlobalSignContactInfo objContactInfo = CloneContact(objPFRequest.GlobalSignOrderRequest.ContactInfoRow!);
                    dbContext.GlobalSignContactInfos.Add(objContactInfo);
                    dbContext.SaveChanges();
                    ContactInfoID = objContactInfo.GlobalSignContactInfoID;
                }

                GlobalSignOrderDetail objGSODetail = new GlobalSignOrderDetail
                {
                    ApprovalEmail = "DNS Verification",
                    DVMethod = "DNS",
                    URLMetaTag = dvc
                };

                var domains = (objPFRequest.CSRDetailRow?.DomainName ?? string.Empty).Split(',');
                objGSODetail.URLVerificationDomains = string.Join(",", domains.Select(d => d.Trim())
                    .Where(d => d != "")
                    .SelectMany(d => new[] { $"http://{d}", $"https://{d}" }));

                string strGSAdditionalDomains = string.Empty;
                if (objPFRequest.GlobalSignOrderRequest.AdditionalDomainsList is Dictionary<string, string> dict)
                    strGSAdditionalDomains = string.Join(",", dict.Keys);

                objGSODetail.StoreOrderId = objPFRequest.StoreOrderDetail.StoreOrderId;
                objGSODetail.CSRDetailId = SCSRinfo;
                if (OrginfoID != 0)
                    objGSODetail.GlobalSignOrganizationInfoID = OrginfoID;
                if (ReqInfoID != 0)
                    objGSODetail.RequestorInfoId = ReqInfoID;
                if (AppInfoID != 0)
                    objGSODetail.ApprovalInfoId = AppInfoID;
                if (AuInfoID != 0)
                    objGSODetail.AuthorizedInfoId = AuInfoID;
                if (ContactInfoID != 0)
                    objGSODetail.ContactInfoId = ContactInfoID;

                dbContext.GlobalSignOrderDetails.Add(objGSODetail);
                dbContext.SaveChanges();

                ChildCertificateDetail chilCertificateDetails = new ChildCertificateDetail
                {
                    StoreOrderId = objPFRequest.StoreOrderDetail.StoreOrderId,
                    SSLCertificateId = objGSOrderResponse.OrderNumber,
                    IsOriginal = true,
                    CertAction = "ISSUED",
                    DateAdded = DateTime.Now,
                    IsFetchStatus = true
                };
                dbContext.ChildCertificateDetails.Add(chilCertificateDetails);
                dbContext.SaveChanges();

                if (!string.IsNullOrEmpty(strGSAdditionalDomains))
                {
                    string[] sans = strGSAdditionalDomains.Split(',');
                    foreach (var item in sans)
                    {
                        if (!string.IsNullOrEmpty(item))
                        {
                            AdditionalDomain objAdditionalDomain = new AdditionalDomain
                            {
                                ApprovalEmail = string.Empty,
                                DomainName = Convert.ToString(item),
                                StoreOrderId = objPFRequest.StoreOrderDetail.StoreOrderId,
                                IsConsiderAsSAN = !(item.ToLower().StartsWith("owa.") || item.ToLower().StartsWith("autodiscover.") || item.ToLower().StartsWith("mail.")),
                                IsPrimaryDomain = false,
                                SpecialNote = string.Empty,
                                CreatedDate = DateTime.Now,
                                UpdatedDate = DateTime.Now
                            };
                            dbContext.AdditionalDomains.Add(objAdditionalDomain);
                        }
                    }
                    dbContext.SaveChanges();
                }

                if (objPFRequest.GlobalSignOrderRequest.WildcardSANDomainList != null &&
                    objPFRequest.GlobalSignOrderRequest.WildcardSANDomainList.Count > 0)
                {
                    foreach (var item in objPFRequest.GlobalSignOrderRequest.WildcardSANDomainList)
                    {
                        dbContext.AdditionalDomains.Add(new AdditionalDomain
                        {
                            ApprovalEmail = string.Empty,
                            DomainName = item,
                            StoreOrderId = objPFRequest.StoreOrderDetail.StoreOrderId,
                            IsConsiderAsSAN = true,
                            IsPrimaryDomain = false,
                            SpecialNote = string.Empty,
                            CreatedDate = DateTime.Now,
                            UpdatedDate = DateTime.Now
                        });
                    }
                    dbContext.SaveChanges();
                }

                dbContext.RenewalOrderDetails.Add(new RenewalOrderDetail
                {
                    ApiOrderNo = objGSOrderResponse.OrderNumber,
                    OrderStatus = "INPROCESS",
                    CompanyName = objPFRequest.StoreOrderDetail.CompanyName,
                    ProductId = objPFRequest.StoreOrderDetail.ProductId,
                    SSLApiLinkId = objPFRequest.StoreOrderDetail.SSLApiLinkId,
                    DomainName = objPFRequest.CSRDetailRow?.DomainName,
                    NewRenewalOrderID = null
                });
                dbContext.SaveChanges();
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }

            StoreMaster? storeMasterDetail = StoreMasterDataAccess.GetById(objPFRequest.StoreOrderDetail.StoreId);
            try
            {
                if (storeMasterDetail != null)
                {
                    var handlerURL = storeMasterDetail.CallBackURL;
                    handlerURL += "?sslapiLinkId=" + objPFRequest.StoreOrderDetail.SSLApiLinkId;
                    handlerURL += "&apiOrderNo=" + objGSOrderResponse.OrderNumber;
                    handlerURL += "&OrderStatus=INPROCESS";
                    handlerURL += "&ProductId=" + objPFRequest.StoreOrderDetail.ProductId;
                    handlerURL += "&CompanyName=" + objPFRequest.StoreOrderDetail.CompanyName;
                    handlerURL += "&DomainName=" + objPFRequest.CSRDetailRow?.DomainName;
                    handlerURL += "&IsAutoConfig=" + objPFRequest.CSRDetailRow?.IsCSRSaved;

                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(handlerURL!);
                    using HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogHandlerError(storeMasterDetail?.StoreName, objPFRequest.StoreOrderDetail.SSLApiLinkId, ex.Message);
            }
        }

        /// <summary>
        /// Same as old BLGlobalsign.SaveGlobslsignOrderInDB (EF Core transaction; UnitOfWork → DbContext).
        /// </summary>
        public static void SaveGlobslsignOrderInDB(PF_Request objPFRequest, OrderResponse objGSOrderResponse)
        {
            if (objPFRequest != null && objPFRequest.IsNewIssue)
            {
                try
                {
                    var result = BLGeneral.GlobalSignDeleteExistingRecordsForIssueCertificate(objPFRequest);
                    if (!result)
                    {
                        LogWriter.LogError("Failed to delete existing records for issue certificate.");
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogErrorDetails(ex);
                    throw new Exception("Delete existing records failed.");
                }
            }

            BLStoreOrder.UpdateAPIOrderNoInStoreOrder(
                objPFRequest!.StoreOrderDetail!.StoreOrderId,
                objGSOrderResponse.OrderNumber,
                objPFRequest.LanguageCode);

            using var dbContext = new SSLConfigurationEntities();
            using var transaction = dbContext.Database.BeginTransaction();
            try
            {
                StoreOrder? storeOrder = StoreOrderDataAccess.GetById(objPFRequest.StoreOrderDetail.StoreOrderId);

                if (storeOrder != null)
                {
                    storeOrder.StoreOrderValidFrom = DateTime.Now;
                    if (storeOrder.StoreOrderValidTo.HasValue && storeOrder.StoreOrderValidTo.Value != DateTime.MinValue)
                    {
                        storeOrder.StoreOrderValidTo = DateTime.Now.AddYears(storeOrder.SubscriptionYear ?? 1);
                    }

                    StoreOrderDataAccess.Update(storeOrder);
                }

                // Save CSRDetails
                CSRDetail objcsrdetail = CloneCsr(objPFRequest.CSRDetailRow!);
                dbContext.CSRDetails.Add(objcsrdetail);
                dbContext.SaveChanges();

                int SCSRinfo = objcsrdetail.CSRDetailId;

                int ReqInfoID = 0;
                int AppInfoID = 0;
                int AuInfoID = 0;
                int ContactInfoID = 0;
                int OrginfoID = 0;

                if (objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow != null
                    && !string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.LegalName)
                    && !string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Address1))
                {
                    // Save Requestor Info
                    if (objPFRequest.GlobalSignOrderRequest.RequestorInfoRow != null
                        && !string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.RequestorInfoRow.FirstName))
                    {
                        GlobalSignContactInfo objRequestorInfo = CloneContact(objPFRequest.GlobalSignOrderRequest.RequestorInfoRow);
                        dbContext.GlobalSignContactInfos.Add(objRequestorInfo);
                        dbContext.SaveChanges();
                        ReqInfoID = objRequestorInfo.GlobalSignContactInfoID;
                    }

                    // Save Approver Info
                    if (objPFRequest.GlobalSignOrderRequest.ApproverInfoRow != null
                        && !string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.ApproverInfoRow.FirstName))
                    {
                        GlobalSignContactInfo objApproverInfo = CloneContact(objPFRequest.GlobalSignOrderRequest.ApproverInfoRow);
                        dbContext.GlobalSignContactInfos.Add(objApproverInfo);
                        dbContext.SaveChanges();
                        AppInfoID = objApproverInfo.GlobalSignContactInfoID;
                    }

                    // Save Authorized Info
                    if (objPFRequest.GlobalSignOrderRequest.AuthorisedInfoRow != null
                        && !string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.AuthorisedInfoRow.FirstName))
                    {
                        GlobalSignContactInfo objAuthorizeInfo = CloneContact(objPFRequest.GlobalSignOrderRequest.AuthorisedInfoRow);
                        dbContext.GlobalSignContactInfos.Add(objAuthorizeInfo);
                        dbContext.SaveChanges();
                        AuInfoID = objAuthorizeInfo.GlobalSignContactInfoID;
                    }

                    // Save Contact Info
                    if (objPFRequest.GlobalSignOrderRequest.ContactInfoRow != null
                        && !string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.ContactInfoRow.FirstName))
                    {
                        GlobalSignContactInfo objContactInfo = CloneContact(objPFRequest.GlobalSignOrderRequest.ContactInfoRow);
                        dbContext.GlobalSignContactInfos.Add(objContactInfo);
                        dbContext.SaveChanges();
                        ContactInfoID = objContactInfo.GlobalSignContactInfoID;
                    }

                    GlobalSignOrganizationInfo objgsoinfo = new GlobalSignOrganizationInfo();
                    objgsoinfo.StoreOrderId = objPFRequest.StoreOrderDetail.StoreOrderId;
                    objgsoinfo.LegalName = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.LegalName) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.LegalName;
                    objgsoinfo.AssumedName = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.AssumedName) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.AssumedName;
                    objgsoinfo.Address1 = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Address1) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Address1;
                    objgsoinfo.Address2 = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Address2) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Address2;
                    objgsoinfo.City = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.City) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.City;
                    objgsoinfo.Country = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Country) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Country;
                    objgsoinfo.Fax = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Fax) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Fax;
                    objgsoinfo.PhoneNo = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.PhoneNo) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.PhoneNo;
                    objgsoinfo.ZipCode = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.ZipCode) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.ZipCode;
                    objgsoinfo.State = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.State) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.State;
                    objgsoinfo.Email = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Email) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Email;
                    objgsoinfo.Duns = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Duns) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Duns;
                    objgsoinfo.Division = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Division) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Division;
                    objgsoinfo.JurictionCity = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionCity) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionCity;
                    objgsoinfo.JurictionCountry = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionCountry) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionCountry;
                    objgsoinfo.JurictionRegNo = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionRegNo) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionRegNo;
                    objgsoinfo.JurictionState = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionState) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionState;
                    objgsoinfo.FirstName = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.FirstName) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.FirstName;
                    objgsoinfo.LastName = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.LastName) ? string.Empty : objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.LastName;

                    dbContext.GlobalSignOrganizationInfos.Add(objgsoinfo);
                    dbContext.SaveChanges();

                    OrginfoID = objgsoinfo.GlobalSignOrganizationInfoID;
                }
                else
                {
                    GlobalSignContactInfo objContactInfo = CloneContact(objPFRequest.GlobalSignOrderRequest.ContactInfoRow!);
                    dbContext.GlobalSignContactInfos.Add(objContactInfo);
                    dbContext.SaveChanges();
                    ContactInfoID = objContactInfo.GlobalSignContactInfoID;
                }

                // Save Order Details
                GlobalSignOrderDetail objGSODetail = new GlobalSignOrderDetail();

                if (!string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.ApprovalMethod) && objPFRequest.GlobalSignOrderRequest.ApprovalMethod.ToLower() == "url")
                {
                    objGSODetail.ApprovalEmail = "URL Verification";
                    objGSODetail.DVMethod = "URL";
                }
                else if (!string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.ApprovalMethod) && objPFRequest.GlobalSignOrderRequest.ApprovalMethod.ToLower() == "dns")
                {
                    objGSODetail.ApprovalEmail = "DNS Verification";
                    objGSODetail.DVMethod = "DNS";
                }
                else
                {
                    objGSODetail.ApprovalEmail = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.ApprovalEmail) ? string.Empty : objPFRequest.GlobalSignOrderRequest.ApprovalEmail;
                    objGSODetail.DVMethod = "EMAIL";
                }

                string strGSAdditionalDomains = string.Empty; // objPFRequest.GlobalSignOrderRequest.AdditionalDomains;

                if (!string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.GS_SAN_OWADomain))
                    strGSAdditionalDomains = objPFRequest.GlobalSignOrderRequest.GS_SAN_OWADomain + ",";

                if (!string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.GS_SAN_AutoDiscoverDomain))
                    strGSAdditionalDomains += objPFRequest.GlobalSignOrderRequest.GS_SAN_AutoDiscoverDomain + ",";

                if (!string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.GS_SAN_MailDomain))
                    strGSAdditionalDomains += objPFRequest.GlobalSignOrderRequest.GS_SAN_MailDomain + ",";

                if (!string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.AdditionalDomains))
                    strGSAdditionalDomains += objPFRequest.GlobalSignOrderRequest.AdditionalDomains;

                objGSODetail.StoreOrderId = objPFRequest.StoreOrderDetail.StoreOrderId;
                objGSODetail.CSRDetailId = SCSRinfo;
                if (OrginfoID != 0)
                {
                    objGSODetail.GlobalSignOrganizationInfoID = OrginfoID;
                }
                if (ReqInfoID != 0)
                {
                    objGSODetail.RequestorInfoId = ReqInfoID;
                }
                if (AppInfoID != 0)
                {
                    objGSODetail.ApprovalInfoId = AppInfoID;
                }
                if (AuInfoID != 0)
                {
                    objGSODetail.AuthorizedInfoId = AuInfoID;
                }
                if (ContactInfoID != 0)
                {
                    objGSODetail.ContactInfoId = ContactInfoID;
                }

                if (!string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.ApprovalMethod) && objPFRequest.GlobalSignOrderRequest.ApprovalMethod.ToLower() == "url")
                {
                    objGSODetail.URLMetaTag = objGSOrderResponse.metatag;

                    string urlVerificationDomains = string.Empty;
                    foreach (var domain in objGSOrderResponse.URLs)
                    {
                        urlVerificationDomains += "http://" + domain + ",";
                        urlVerificationDomains += "https://" + domain + ",";
                    }
                    objGSODetail.URLVerificationDomains = urlVerificationDomains.TrimEnd(',');
                }
                else if (!string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.ApprovalMethod) && objPFRequest.GlobalSignOrderRequest.ApprovalMethod.ToLower() == "dns")
                {
                    objGSODetail.URLMetaTag = objGSOrderResponse.DNSText;

                    string dnsVerificationDomains = string.Empty;
                    foreach (var domain in objGSOrderResponse.DNSVerificaitonDomains)
                    {
                        dnsVerificationDomains += domain + ",";
                    }
                    objGSODetail.URLVerificationDomains = dnsVerificationDomains.TrimEnd(',');
                }
                objGSODetail.PickupPassword = string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.PickupPassword)
                    ? string.Empty
                    : Crypto.Encrypt(objPFRequest.GlobalSignOrderRequest.PickupPassword);

                dbContext.GlobalSignOrderDetails.Add(objGSODetail);
                dbContext.SaveChanges();

                // save child certificate details
                dbContext.ChildCertificateDetails.Add(new ChildCertificateDetail
                {
                    StoreOrderId = objPFRequest.StoreOrderDetail.StoreOrderId,
                    SSLCertificateId = objGSOrderResponse.OrderNumber,
                    IsOriginal = true,
                    CertAction = "ISSUED",
                    DateAdded = DateTime.Now,
                    IsFetchStatus = true
                });
                dbContext.SaveChanges();

                #region New code for Additional Domains on 20-Jan-2016

                // check for empty addtional domains
                if (!string.IsNullOrEmpty(strGSAdditionalDomains))
                {
                    // split all addtional domains
                    string[] sans = strGSAdditionalDomains.Split(',');

                    foreach (var item in sans)
                    {
                        // save to database
                        if (!string.IsNullOrEmpty(item))
                        {
                            AdditionalDomain objAdditionalDomain = new AdditionalDomain();

                            objAdditionalDomain.ApprovalEmail = string.Empty;
                            objAdditionalDomain.DomainName = Convert.ToString(item);
                            objAdditionalDomain.StoreOrderId = objPFRequest.StoreOrderDetail.StoreOrderId;

                            if (item.ToLower().StartsWith("owa.") || item.ToLower().StartsWith("autodiscover.") || item.ToLower().StartsWith("mail."))
                            {
                                objAdditionalDomain.IsConsiderAsSAN = false;
                            }
                            else
                            {
                                objAdditionalDomain.IsConsiderAsSAN = true;
                            }

                            objAdditionalDomain.IsPrimaryDomain = false;
                            objAdditionalDomain.SpecialNote = string.Empty;
                            objAdditionalDomain.CreatedDate = DateTime.Now;
                            objAdditionalDomain.UpdatedDate = DateTime.Now;

                            dbContext.AdditionalDomains.Add(objAdditionalDomain);
                        }
                    }
                    dbContext.SaveChanges();
                }

                #endregion

                // add wildcard san
                if (objPFRequest.GlobalSignOrderRequest.WildcardSANDomainList != null && objPFRequest.GlobalSignOrderRequest.WildcardSANDomainList.Count > 0)
                {
                    foreach (var item in objPFRequest.GlobalSignOrderRequest.WildcardSANDomainList)
                    {
                        AdditionalDomain objAdditionalDomain = new AdditionalDomain();

                        objAdditionalDomain.ApprovalEmail = string.Empty;
                        objAdditionalDomain.DomainName = item;
                        objAdditionalDomain.StoreOrderId = objPFRequest.StoreOrderDetail.StoreOrderId;
                        objAdditionalDomain.IsConsiderAsSAN = true;
                        objAdditionalDomain.IsPrimaryDomain = false;
                        objAdditionalDomain.SpecialNote = string.Empty;
                        objAdditionalDomain.CreatedDate = DateTime.Now;
                        objAdditionalDomain.UpdatedDate = DateTime.Now;

                        dbContext.AdditionalDomains.Add(objAdditionalDomain);
                    }
                    dbContext.SaveChanges();
                }

                // Save into RenewalOrderDetail
                dbContext.RenewalOrderDetails.Add(new RenewalOrderDetail
                {
                    ApiOrderNo = objGSOrderResponse.OrderNumber,
                    OrderStatus = "INPROCESS",
                    CompanyName = objPFRequest.StoreOrderDetail.CompanyName,
                    ProductId = objPFRequest.StoreOrderDetail.ProductId,
                    SSLApiLinkId = objPFRequest.StoreOrderDetail.SSLApiLinkId,
                    DomainName = objPFRequest.CSRDetailRow?.DomainName,
                    NewRenewalOrderID = null
                });

                dbContext.SaveChanges();
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }

            // Save Renewal Order Detail at respective Store Database..
            // Thin layer architecture: fetch StoreMaster via StoreMasterDataAccess
            StoreMaster? storeMasterDetail = StoreMasterDataAccess.GetById(objPFRequest.StoreOrderDetail.StoreId);
            try
            {
                if (storeMasterDetail != null)
                {
                    var handlerURL = storeMasterDetail.CallBackURL;

                    handlerURL += "?sslapiLinkId=" + objPFRequest.StoreOrderDetail.SSLApiLinkId;
                    handlerURL += "&apiOrderNo=" + objGSOrderResponse.OrderNumber;
                    //handlerURL += "&StartDate=" + objPFRequest.StoreOrderDetail.ApiOrderNo;
                    //handlerURL += "&EndDate=" + objPFRequest.StoreOrderDetail.ApiOrderNo;
                    handlerURL += "&OrderStatus=INPROCESS";
                    handlerURL += "&ProductId=" + objPFRequest.StoreOrderDetail.ProductId;
                    handlerURL += "&CompanyName=" + objPFRequest.StoreOrderDetail.CompanyName;
                    handlerURL += "&DomainName=" + objPFRequest.CSRDetailRow?.DomainName;
                    handlerURL += "&IsAutoConfig=" + objPFRequest.CSRDetailRow?.IsCSRSaved;

                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(handlerURL!);
                    using HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                }
            }
            catch (Exception ex)
            {
                LogWriter.LogHandlerError(storeMasterDetail?.StoreName, objPFRequest.StoreOrderDetail.SSLApiLinkId, ex.Message);
            }
        }

        private static string RemoveSANTypeFromSAN(string AdditionalDomains)
        {
            string strReturn = string.Empty;
            if (AdditionalDomains.Contains(","))
            {
                string[] arrStr = AdditionalDomains.Split(',');
                foreach (string str in arrStr)
                {
                    strReturn += str.Split('|')[1] + ",";
                }
            }

            return strReturn;
        }

        private static CSRDetail CloneCsr(CSRDetail src) => new CSRDetail
        {
            DomainName = src.DomainName,
            Country = src.Country,
            Locality = src.Locality,
            Organisation = src.Organisation,
            OrganisationUnit = src.OrganisationUnit,
            State = src.State,
            Email = src.Email,
            CSR = src.CSR,
            IsCSRSaved = src.IsCSRSaved,
            DNSNames = src.DNSNames
        };

        private static GlobalSignContactInfo CloneContact(GlobalSignContactInfo src) => new GlobalSignContactInfo
        {
            OrganizationName = src.OrganizationName,
            FirstName = src.FirstName,
            LastName = src.LastName,
            PhoneNo = src.PhoneNo,
            Email = src.Email,
            OrgUnit = src.OrgUnit,
            FunctionInOrg = src.FunctionInOrg,
            Address1 = src.Address1,
            Address2 = src.Address2,
            City = src.City,
            State = src.State,
            Country = src.Country,
            ZipCode = src.ZipCode,
            Duns = src.Duns,
            Title = src.Title
        };

        #endregion
    }
}
