using System.Text.Json;
using SSLConfiguration.Infrastructure;
using SSLConfiguration.Infrastructure.Persistence;
using VerisignGateway;
using VerisignGateway.GS_MarkService;

namespace SSLConfiguration.Application
{
    /// <summary>
    /// GlobalSign Summary / PlaceOrder — same gateway paths as old ProductFactory GlobalSign* / AlphaSSL PlaceOrder
    /// (VerisignUtil.GetGSProductObject + QuickGSOrder / Url / DNS / CodeSign).
    /// </summary>
    public static class GlobalSignPlaceOrder
    {
        public static PF_Response PlaceOrder(PF_Request objPFRequest)
        {
            VerisignGateway.ProductCode productCode =
                (VerisignGateway.ProductCode)objPFRequest.StoreOrderDetail!.ProductId;

            switch (productCode)
            {
                case VerisignGateway.ProductCode.GSCodeSign:
                case VerisignGateway.ProductCode.GSEVCodeSign:
                    return PlaceCodeSignOrder(objPFRequest, productCode);

                case VerisignGateway.ProductCode.AlphaSSL:
                case VerisignGateway.ProductCode.AlphaWildCard:
                case VerisignGateway.ProductCode.GSDomainSSL:
                case VerisignGateway.ProductCode.GSDomainWildcardSSL:
                case VerisignGateway.ProductCode.GSDomainSSLMD:
                    return PlaceDvOrder(objPFRequest, productCode);

                case VerisignGateway.ProductCode.GSOrganizationSSL:
                case VerisignGateway.ProductCode.GSOrganizationSSLMD:
                case VerisignGateway.ProductCode.GSOrganizationWildcardSSL:
                    return PlaceOrganizationOrder(objPFRequest, productCode);

                case VerisignGateway.ProductCode.GSExtendedValidationSSL:
                case VerisignGateway.ProductCode.GSExtendedValidationSSLMD:
                    return PlaceEvOrder(objPFRequest, productCode);

                case VerisignGateway.ProductCode.PrimeSSLVerifiedMarkCertificate:
                case VerisignGateway.ProductCode.PrimeSSLCommonMarkCertificate:
                    return PlaceVmcOrder(objPFRequest, productCode);

                default:
                    return PlaceDvOrder(objPFRequest, productCode);
            }
        }

        /// <summary>
        /// Same as old GlobalSignCodeSign / GlobalSignEVCodeSign.PlaceOrder.
        /// </summary>
        public static PF_Response PlaceCodeSignOrder(PF_Request objPFRequest, VerisignGateway.ProductCode productCode)
        {
            PF_Response objReturn = new PF_Response();

            try
            {
                bool isLinkUsed = BLStoreOrder.CheckConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail!.StoreOrderId);

                if (isLinkUsed)
                {
                    objReturn.ErrorCode = -1000;
                    objReturn.ErrorFiled = "PIN";
                    objReturn.ErrorMessage = "Link is already used.";
                    return objReturn;
                }

                BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, true);

                ProductBase objProd = VerisignUtil.GetGSProductObject(productCode);
                GSCodeSignOrderRequest objGSOrderRequest = BLGlobalsign.GetGSCodeSignOrderRequest(objPFRequest);

                OrderResponse? objGSOrderResponse = null;
                try
                {
                    objGSOrderResponse = objProd.QuickGSCodeSignOrder(objGSOrderRequest);
                }
                catch (Exception ex)
                {
                    LogWriter.LogRequestResponseObjectToDB(objPFRequest, ex); // system Error log to LogMaster
                    BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, false);
                    throw;
                }

                if (objGSOrderResponse.error != null && objGSOrderResponse.error.ErrorCode < 0)
                {
                    LogWriter.LogCARequestResponseObjectToDB(objPFRequest.StoreOrderDetail.Pin, objGSOrderResponse); // CA Error log to LogMaster
                    LogWriter.LogGlobalSignOrderRequestObjectsToDB(objPFRequest);
                    BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, false);
                    BLGlobalsign.SetErrorFromGlobalSign(objReturn, objGSOrderResponse);
                    return objReturn;
                }
                else
                {
                    objReturn.VendorID = objGSOrderResponse.OrderNumber;
                    objReturn.ApprovalEmail = objPFRequest.GlobalSignOrderRequest.ApprovalEmail;
                }

                BLGlobalsign.SaveGlobslsignOrderInDB(objPFRequest, objGSOrderResponse);
            }
            catch (Exception ex)
            {
                LogWriter.LogRequestResponseObjectToDB(objPFRequest, ex); // system Error log to LogMaster
                throw;
            }

            return objReturn;
        }

        /// <summary>
        /// Same as old AlphaSSL / AlphaWildcard / GlobalSignDomainSSL / DomainWildcard / Domain_SANSSL.PlaceOrder.
        /// </summary>
        private static PF_Response PlaceDvOrder(PF_Request objPFRequest, VerisignGateway.ProductCode productCode)
        {
            PF_Response objReturn = new PF_Response();

            try
            {
                bool isLinkUsed = BLStoreOrder.CheckConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail!.StoreOrderId);

                if (isLinkUsed)
                {
                    objReturn.ErrorCode = -1000;
                    objReturn.ErrorFiled = "PIN";
                    objReturn.ErrorMessage = "Link is already used.";
                    return objReturn;
                }

                BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, true);

                ProductBase objProd = VerisignUtil.GetGSProductObject(productCode);
                GSOrderRequest objGSOrderRequest = BLGlobalsign.GetGSOrderRequest(objPFRequest);

                ApplyUcSans(objPFRequest, objGSOrderRequest, productCode);
                ApplyDomainSans(objPFRequest, objGSOrderRequest, productCode);

                OrderResponse? objGSOrderResponse = null;

                //bool isURLVerification = objPFRequest.GlobalSignOrderRequest.ApprovalMethod.ToLower() == "url" ? true : false;
                string approvalMethod = (objPFRequest.GlobalSignOrderRequest.ApprovalMethod ?? string.Empty).ToLower();

                try
                {
                    //if (isURLVerification)
                    //    objGSOrderResponse = objProd.QuickGSUrlVerificationOrder(objGSOrderRequest);
                    //else
                    //    objGSOrderResponse = objProd.QuickGSOrder(objGSOrderRequest);

                    if (approvalMethod == "url")
                    {
                        objGSOrderResponse = objProd.QuickGSUrlVerificationOrder(objGSOrderRequest);
                    }
                    else if (approvalMethod == "dns")
                    {
                        objGSOrderResponse = objProd.QuickGSDNSVerificationOrder(objGSOrderRequest);
                    }
                    else
                    {
                        objGSOrderResponse = objProd.QuickGSOrder(objGSOrderRequest);
                    }
                }
                catch (Exception ex)
                {
                    LogWriter.LogRequestResponseObjectToDB(objPFRequest, ex); // system Error log to LogMaster
                    BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, false);
                    throw;
                }

                /*
                bool isURLVerification = objPFRequest.GlobalSignOrderRequest.ApprovalMethod.ToLower() == "url" ? true : false;

                try
                {
                    if (isURLVerification)
                        objGSOrderResponse = objProd.QuickGSUrlVerificationOrder(objGSOrderRequest);
                    else
                        objGSOrderResponse = objProd.QuickGSOrder(objGSOrderRequest);
                }
                catch(Exception ex)
                {
                    BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, false);
                    throw (ex);
                }
                */

                if (objGSOrderResponse.error != null && objGSOrderResponse.error.ErrorCode < 0)
                {
                    LogWriter.LogCARequestResponseObjectToDB(objPFRequest.StoreOrderDetail.Pin, objGSOrderResponse); // CA Error log to LogMaster
                    LogWriter.LogGlobalSignOrderRequestObjectsToDB(objPFRequest);
                    BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, false);
                    BLGlobalsign.SetErrorFromGlobalSign(objReturn, objGSOrderResponse);
                    return objReturn;
                }
                else
                {
                    objReturn.VendorID = objGSOrderResponse.OrderNumber;
                    objReturn.ApprovalEmail = objPFRequest.GlobalSignOrderRequest.ApprovalEmail;

                    /*
                    if (isURLVerification)
                    {
                        objReturn.GSURLMetaTag = objGSOrderResponse.metatag;
                        objReturn.GSURLs = objGSOrderResponse.URLs;
                    }*/

                    //if (isURLVerification)
                    //{
                    //    objReturn.GSURLMetaTag = objGSOrderResponse.metatag;
                    //    objReturn.GSURLs = objGSOrderResponse.URLs;
                    //}

                    if (approvalMethod == "url")
                    {
                        objReturn.GSURLMetaTag = objGSOrderResponse.metatag;
                        objReturn.GSURLs = objGSOrderResponse.URLs;
                    }
                    else if (approvalMethod == "dns")
                    {
                        objReturn.GSURLMetaTag = objGSOrderResponse.DNSText;
                        objReturn.GSURLs = objGSOrderResponse.DNSVerificaitonDomains;
                    }
                }

                BLGlobalsign.SaveGlobslsignOrderInDB(objPFRequest, objGSOrderResponse);
            }
            catch (Exception ex)
            {
                LogWriter.LogRequestResponseObjectToDB(objPFRequest, ex); // system Error log to LogMaster
                throw;
            }

            return objReturn;
        }

        /// <summary>
        /// Same as old GlobalSignOrganizationSSL / Organisation_SANSSL / OrganisationWildcard.PlaceOrder.
        /// </summary>
        private static PF_Response PlaceOrganizationOrder(PF_Request objPFRequest, VerisignGateway.ProductCode productCode)
        {
            PF_Response objReturn = new PF_Response();

            try
            {
                bool isLinkUsed = BLStoreOrder.CheckConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail!.StoreOrderId);

                if (isLinkUsed)
                {
                    objReturn.ErrorCode = -1000;
                    objReturn.ErrorFiled = "PIN";
                    objReturn.ErrorMessage = "Link is already used.";
                    return objReturn;
                }

                BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, true);

                ProductBase objProd = VerisignUtil.GetGSProductObject(productCode);
                GSOrderRequest objGSOrderRequest = BLGlobalsign.GetGSOrderRequest(objPFRequest);

                ApplyUcSans(objPFRequest, objGSOrderRequest, productCode);
                ApplyOrganizationSans(objPFRequest, objGSOrderRequest, productCode);
                ApplyOrganisationDetailOv(objPFRequest, objGSOrderRequest, productCode);

                OrderResponse? objGSOrderResponse = null;
                try
                {
                    objGSOrderResponse = objProd.QuickGSOrder(objGSOrderRequest);
                }
                catch (Exception ex)
                {
                    LogWriter.LogRequestResponseObjectToDB(objPFRequest, ex); // system Error log to LogMaster
                    BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, false);
                    throw;
                }

                if (objGSOrderResponse.error != null && objGSOrderResponse.error.ErrorCode < 0)
                {
                    LogWriter.LogCARequestResponseObjectToDB(objPFRequest.StoreOrderDetail.Pin, objGSOrderResponse); // CA Error log to LogMaster
                    LogWriter.LogGlobalSignOrderRequestObjectsToDB(objPFRequest);
                    BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, false);
                    BLGlobalsign.SetErrorFromGlobalSign(objReturn, objGSOrderResponse);
                    return objReturn;
                }
                else
                {
                    objReturn.VendorID = objGSOrderResponse.OrderNumber;
                    objReturn.ApprovalEmail = objPFRequest.GlobalSignOrderRequest.ApprovalEmail;
                }

                BLGlobalsign.SaveGlobslsignOrderInDB(objPFRequest, objGSOrderResponse);
            }
            catch (Exception ex)
            {
                LogWriter.LogRequestResponseObjectToDB(objPFRequest, ex); // system Error log to LogMaster
                throw;
            }

            return objReturn;
        }

        /// <summary>
        /// Same as old GlobalSignEVSSL / GlobalSignEV_SAN.PlaceOrder.
        /// </summary>
        private static PF_Response PlaceEvOrder(PF_Request objPFRequest, VerisignGateway.ProductCode productCode)
        {
            PF_Response objReturn = new PF_Response();

            try
            {
                bool isLinkUsed = BLStoreOrder.CheckConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail!.StoreOrderId);

                if (isLinkUsed)
                {
                    objReturn.ErrorCode = -1000;
                    objReturn.ErrorFiled = "PIN";
                    objReturn.ErrorMessage = "Link is already used.";
                    return objReturn;
                }

                BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, true);

                ProductBase objProd = VerisignUtil.GetGSProductObject(productCode);
                GSOrderRequest objGSOrderRequest = BLGlobalsign.GetGSOrderRequest(objPFRequest);

                ApplyUcSans(objPFRequest, objGSOrderRequest, productCode);
                ApplyEvSans(objPFRequest, objGSOrderRequest, productCode);
                ApplyOrganisationDetailEv(objPFRequest, objGSOrderRequest);
                ApplyEvContacts(objPFRequest, objGSOrderRequest);

                OrderResponse? objGSOrderResponse = null;

                try
                {
                    objGSOrderResponse = objProd.QuickGSOrder(objGSOrderRequest);
                }
                catch (Exception ex)
                {
                    LogWriter.LogRequestResponseObjectToDB(objPFRequest, ex); // system Error log to LogMaster
                    BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, false);
                    throw;
                }

                if (objGSOrderResponse.error != null && objGSOrderResponse.error.ErrorCode < 0)
                {
                    LogWriter.LogCARequestResponseObjectToDB(objPFRequest.StoreOrderDetail.Pin, objGSOrderResponse); // CA Error log to LogMaster
                    LogWriter.LogGlobalSignOrderRequestObjectsToDB(objPFRequest);
                    BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, false);
                    BLGlobalsign.SetErrorFromGlobalSign(objReturn, objGSOrderResponse);
                    return objReturn;
                }
                else
                {
                    objReturn.VendorID = objGSOrderResponse.OrderNumber;
                    objReturn.ApprovalEmail = objPFRequest.GlobalSignOrderRequest.ApprovalEmail;
                }

                BLGlobalsign.SaveGlobslsignOrderInDB(objPFRequest, objGSOrderResponse);
            }
            catch (Exception ex)
            {
                LogWriter.LogRequestResponseObjectToDB(objPFRequest, ex); // system Error log to LogMaster
                throw;
            }

            return objReturn;
        }

        private static void ApplyUcSans(PF_Request objPFRequest, GSOrderRequest objGSOrderRequest, VerisignGateway.ProductCode productCode)
        {
            // OrganisationWildcard does not apply UC SANs in old factory
            if (productCode == VerisignGateway.ProductCode.GSOrganizationWildcardSSL
                || productCode == VerisignGateway.ProductCode.AlphaSSL
                || productCode == VerisignGateway.ProductCode.AlphaWildCard
                || productCode == VerisignGateway.ProductCode.GSDomainWildcardSSL)
            {
                return;
            }

            List<GSAddDomain> ary = objGSOrderRequest.SAN != null
                ? new List<GSAddDomain>(objGSOrderRequest.SAN)
                : new List<GSAddDomain>();

            if (!string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.GS_SAN_MailDomain))
            {
                GSAddDomain objgsadd = new GSAddDomain();
                objgsadd.DomainName = objPFRequest.GlobalSignOrderRequest.GS_SAN_MailDomain;
                objgsadd.SANType = GSSANType.UC_SAN;
                ary.Add(objgsadd);
            }

            if (!string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.GS_SAN_OWADomain))
            {
                GSAddDomain objgsadd = new GSAddDomain();
                objgsadd.DomainName = objPFRequest.GlobalSignOrderRequest.GS_SAN_OWADomain;
                objgsadd.SANType = GSSANType.UC_SAN;
                ary.Add(objgsadd);
            }

            if (!string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.GS_SAN_AutoDiscoverDomain))
            {
                GSAddDomain objgsadd = new GSAddDomain();
                objgsadd.DomainName = objPFRequest.GlobalSignOrderRequest.GS_SAN_AutoDiscoverDomain;
                objgsadd.SANType = GSSANType.UC_SAN;
                ary.Add(objgsadd);
            }

            if (ary != null && ary.Count > 0)
                objGSOrderRequest.SAN = ary.ToArray();
        }

        private static void ApplyDomainSans(PF_Request objPFRequest, GSOrderRequest objGSOrderRequest, VerisignGateway.ProductCode productCode)
        {
            if (productCode != VerisignGateway.ProductCode.GSDomainSSLMD)
                return;

            List<GSAddDomain> ary = objGSOrderRequest.SAN != null
                ? new List<GSAddDomain>(objGSOrderRequest.SAN)
                : new List<GSAddDomain>();

            if (!string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.AdditionalDomains))
            {
                string AD = objPFRequest.GlobalSignOrderRequest.AdditionalDomains;
                char[] separator = new char[] { ',' };
                string[] strAD = AD.Split(separator);
                foreach (string GetAD in strAD)
                {
                    if (!string.IsNullOrEmpty(GetAD))
                    {
                        GSAddDomain objgsadd = new GSAddDomain();
                        objgsadd.DomainName = GetAD;
                        objgsadd.SANType = GSSANType.SubDomain;
                        ary.Add(objgsadd);
                    }
                }
            }

            if (ary != null && ary.Count > 0)
                objGSOrderRequest.SAN = ary.ToArray();
        }

        private static void ApplyOrganizationSans(PF_Request objPFRequest, GSOrderRequest objGSOrderRequest, VerisignGateway.ProductCode productCode)
        {
            List<GSAddDomain> ary = objGSOrderRequest.SAN != null
                ? new List<GSAddDomain>(objGSOrderRequest.SAN)
                : new List<GSAddDomain>();

            if (productCode == VerisignGateway.ProductCode.GSOrganizationWildcardSSL)
            {
                if (!string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.AdditionalDomains))
                {
                    string AD = objPFRequest.GlobalSignOrderRequest.AdditionalDomains;
                    char[] separator = new char[] { ',' };
                    string[] strAD = AD.Split(separator);
                    foreach (string GetAD in strAD)
                    {
                        GSAddDomain objgsadd = new GSAddDomain();
                        objgsadd.DomainName = GetAD;
                        objgsadd.SANType = GSSANType.WildcardSAN;
                        ary.Add(objgsadd);

                        // Add free base domain without *. for each wildcard san
                        GSAddDomain freeSAN = new GSAddDomain();
                        freeSAN.DomainName = GetAD.Replace("*.", "");
                        freeSAN.SANType = GSSANType.FQDN;
                        ary.Add(freeSAN);
                    }
                }
            }
            else if (productCode == VerisignGateway.ProductCode.GSOrganizationSSLMD)
            {
                if (!string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.AdditionalDomains))
                {
                    string AD = objPFRequest.GlobalSignOrderRequest.AdditionalDomains;
                    char[] separator = new char[] { ',' };
                    string[] strAD = AD.Split(separator);
                    foreach (string GetAD in strAD)
                    {
                        char[] sep = new char[] { '|' };
                        string[] strservervalue = GetAD.Split(sep);

                        GSAddDomain objgsadd = new GSAddDomain();
                        objgsadd.DomainName = strservervalue[1];
                        if (strservervalue[0] == "FQDN")
                        {
                            objgsadd.SANType = GSSANType.FQDN;
                        }
                        if (strservervalue[0] == "SubDomain")
                        {
                            objgsadd.SANType = GSSANType.SubDomain;
                        }
                        ary.Add(objgsadd);
                    }
                }

                // add Wildcard SANs
                if (objPFRequest.GlobalSignOrderRequest.WildcardSANDomainList != null && objPFRequest.GlobalSignOrderRequest.WildcardSANDomainList.Count > 0)
                {
                    if (ary == null)
                        ary = new List<GSAddDomain>();

                    foreach (string san in objPFRequest.GlobalSignOrderRequest.WildcardSANDomainList)
                    {
                        GSAddDomain objgsadd = new GSAddDomain();
                        objgsadd.DomainName = san;
                        objgsadd.SANType = GSSANType.WildcardSAN;

                        ary.Add(objgsadd);

                        // Add free base domain without *. for each wildcard san
                        GSAddDomain freeSAN = new GSAddDomain();
                        freeSAN.DomainName = san.Replace("*.", "");
                        freeSAN.SANType = GSSANType.FQDN;
                        ary.Add(freeSAN);
                    }
                }
            }

            if (ary != null && ary.Count > 0)
                objGSOrderRequest.SAN = ary.ToArray();
        }

        private static void ApplyEvSans(PF_Request objPFRequest, GSOrderRequest objGSOrderRequest, VerisignGateway.ProductCode productCode)
        {
            if (productCode != VerisignGateway.ProductCode.GSExtendedValidationSSLMD)
                return;

            List<GSAddDomain> ary = objGSOrderRequest.SAN != null
                ? new List<GSAddDomain>(objGSOrderRequest.SAN)
                : new List<GSAddDomain>();

            if (!string.IsNullOrEmpty(objPFRequest.GlobalSignOrderRequest.AdditionalDomains))
            {
                string AD = objPFRequest.GlobalSignOrderRequest.AdditionalDomains;
                char[] separator = new char[] { ',' };
                string[] strAD = AD.Split(separator);
                foreach (string GetAD in strAD)
                {
                    char[] sep = new char[] { '|' };
                    string[] strservervalue = GetAD.Split(sep);

                    GSAddDomain objgsadd = new GSAddDomain();
                    objgsadd.DomainName = strservervalue[1];
                    if (strservervalue[0].ToUpper() == "FQDN")
                    {
                        objgsadd.SANType = GSSANType.FQDN;
                    }
                    if (strservervalue[0].ToUpper() == "INTERNALHOSTNAME")
                    {
                        objgsadd.SANType = GSSANType.InternalHostName;
                    }
                    if (strservervalue[0].ToUpper() == "PUBLICIP")
                    {
                        objgsadd.SANType = GSSANType.PublicIP;
                    }
                    if (strservervalue[0].ToUpper() == "SUBDOMAIN")
                    {
                        objgsadd.SANType = GSSANType.SubDomain;
                    }
                    if (strservervalue[0].ToUpper() == "UC_SAN")
                    {
                        objgsadd.SANType = GSSANType.UC_SAN;
                    }
                    ary.Add(objgsadd);
                }
            }

            if (ary != null && ary.Count > 0)
                objGSOrderRequest.SAN = ary.ToArray();
        }

        private static void ApplyOrganisationDetailOv(PF_Request objPFRequest, GSOrderRequest objGSOrderRequest, VerisignGateway.ProductCode productCode)
        {
            #region Set OrgranisationInfo

            if (objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow != null)
            {
                //GlobalSignOrgInfo objTempOrg = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow;
                objGSOrderRequest.OrganisationDetail = new GSOrganizationInfo();

                objGSOrderRequest.OrganisationDetail.OrganizationAddress = new Address();
                objGSOrderRequest.OrganisationDetail.OrganizationAddress.AddressLine1 = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Address1;
                objGSOrderRequest.OrganisationDetail.OrganizationAddress.AddressLine2 = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Address2;
                objGSOrderRequest.OrganisationDetail.OrganizationAddress.City = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.City;
                objGSOrderRequest.OrganisationDetail.OrganizationAddress.Country = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Country;
                objGSOrderRequest.OrganisationDetail.OrganizationAddress.Fax = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Fax;
                objGSOrderRequest.OrganisationDetail.OrganizationAddress.Phone = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.PhoneNo;
                objGSOrderRequest.OrganisationDetail.OrganizationAddress.PostalCode = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.ZipCode;
                objGSOrderRequest.OrganisationDetail.OrganizationAddress.State = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.State;

                objGSOrderRequest.OrganisationDetail.OrganizationEmail = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Email;
                objGSOrderRequest.OrganisationDetail.OrganizationName = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.LegalName;

                if (productCode != VerisignGateway.ProductCode.GSOrganizationWildcardSSL)
                    objGSOrderRequest.OrganisationDetail.OrganizationDUNS = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Duns;
            }

            #endregion
        }

        private static void ApplyOrganisationDetailEv(PF_Request objPFRequest, GSOrderRequest objGSOrderRequest)
        {
            #region Set OrgranisationInfo and Jurction Info

            if (objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow != null)
            {
                //GlobalSignOrgInfo objTempOrg = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow;
                objGSOrderRequest.OrganisationDetail = new GSOrganizationInfo();

                objGSOrderRequest.OrganisationDetail.OrganizationAddress = new Address();
                objGSOrderRequest.OrganisationDetail.OrganizationAddress.AddressLine1 = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Address1;
                objGSOrderRequest.OrganisationDetail.OrganizationAddress.AddressLine2 = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Address2;
                objGSOrderRequest.OrganisationDetail.OrganizationAddress.City = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.City;
                objGSOrderRequest.OrganisationDetail.OrganizationAddress.Country = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Country;
                objGSOrderRequest.OrganisationDetail.OrganizationAddress.Fax = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Fax;
                objGSOrderRequest.OrganisationDetail.OrganizationAddress.Phone = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.PhoneNo;
                objGSOrderRequest.OrganisationDetail.OrganizationAddress.PostalCode = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.ZipCode;
                objGSOrderRequest.OrganisationDetail.OrganizationAddress.State = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.State;

                objGSOrderRequest.OrganisationDetail.OrganizationAssumedName = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.AssumedName;
                objGSOrderRequest.OrganisationDetail.OrganizationDivision = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Division;
                objGSOrderRequest.OrganisationDetail.OrganizationDUNS = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Duns;
                objGSOrderRequest.OrganisationDetail.OrganizationEmail = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.Email;
                objGSOrderRequest.OrganisationDetail.OrganizationName = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.LegalName;

                objGSOrderRequest.OrganisationDetail.OrgType = (GSOrganizationType)Convert.ToInt32(objPFRequest.GlobalSignOrderRequest.enmOrganisaztionType);

                objGSOrderRequest.OrganisationDetail.JuridictionInfo = new VerisignGateway.GSJuridctionInfo();
                objGSOrderRequest.OrganisationDetail.JuridictionInfo.AgencyRegNumber = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionRegNo;
                objGSOrderRequest.OrganisationDetail.JuridictionInfo.Country = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionCountry;
                objGSOrderRequest.OrganisationDetail.JuridictionInfo.Locality = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionCity;
                objGSOrderRequest.OrganisationDetail.JuridictionInfo.State = objPFRequest.GlobalSignOrderRequest.OrganisationInfoRow.JurictionState;
            }

            #endregion
        }

        private static void ApplyEvContacts(PF_Request objPFRequest, GSOrderRequest objGSOrderRequest)
        {
            #region Set Different Contact Info for EV Only

            GlobalSignContactInfo? objTempContactInfo = null;

            if (objPFRequest.GlobalSignOrderRequest.ApproverInfoRow != null)
            {
                objTempContactInfo = objPFRequest.GlobalSignOrderRequest.ApproverInfoRow;
                objGSOrderRequest.ApproverInfo = new ContactDetail();

                objGSOrderRequest.ApproverInfo.Email = objTempContactInfo.Email;
                objGSOrderRequest.ApproverInfo.FirstName = objTempContactInfo.FirstName;
                objGSOrderRequest.ApproverInfo.FunctionInOrg = objTempContactInfo.FunctionInOrg;
                objGSOrderRequest.ApproverInfo.LastName = objTempContactInfo.LastName;
                objGSOrderRequest.ApproverInfo.OrganizationName = objTempContactInfo.OrganizationName;
                objGSOrderRequest.ApproverInfo.OrganizationUnit = objTempContactInfo.OrgUnit;
                objGSOrderRequest.ApproverInfo.Phone = objTempContactInfo.PhoneNo;
            }

            if (objPFRequest.GlobalSignOrderRequest.AuthorisedInfoRow != null)
            {
                objTempContactInfo = objPFRequest.GlobalSignOrderRequest.AuthorisedInfoRow;

                objGSOrderRequest.AuthoiizedSignerInfo = new ContactDetail();
                objGSOrderRequest.AuthoiizedSignerInfo.Email = objTempContactInfo.Email;
                objGSOrderRequest.AuthoiizedSignerInfo.FirstName = objTempContactInfo.FirstName;
                objGSOrderRequest.AuthoiizedSignerInfo.FunctionInOrg = objTempContactInfo.FunctionInOrg;
                objGSOrderRequest.AuthoiizedSignerInfo.LastName = objTempContactInfo.LastName;
                objGSOrderRequest.AuthoiizedSignerInfo.OrganizationName = objTempContactInfo.OrganizationName;
                objGSOrderRequest.AuthoiizedSignerInfo.OrganizationUnit = objTempContactInfo.OrgUnit;
                objGSOrderRequest.AuthoiizedSignerInfo.Phone = objTempContactInfo.PhoneNo;
            }

            if (objPFRequest.GlobalSignOrderRequest.RequestorInfoRow != null)
            {
                objTempContactInfo = objPFRequest.GlobalSignOrderRequest.RequestorInfoRow;

                objGSOrderRequest.ReqeustorInfo = new ContactDetail();
                objGSOrderRequest.ReqeustorInfo.Email = objTempContactInfo.Email;
                objGSOrderRequest.ReqeustorInfo.FirstName = objTempContactInfo.FirstName;
                objGSOrderRequest.ReqeustorInfo.FunctionInOrg = objTempContactInfo.FunctionInOrg;
                objGSOrderRequest.ReqeustorInfo.LastName = objTempContactInfo.LastName;
                objGSOrderRequest.ReqeustorInfo.OrganizationName = objTempContactInfo.OrganizationName;
                objGSOrderRequest.ReqeustorInfo.OrganizationUnit = objTempContactInfo.OrgUnit;
                objGSOrderRequest.ReqeustorInfo.Phone = objTempContactInfo.PhoneNo;
            }

            #endregion
        }

        /// <summary>
        /// Same as old PrimeSSL_VerifiedMarkCertificate / PrimeSSL_CommonMarkCertificate PlaceOrder.
        /// </summary>
        public static PF_Response PlaceVmcOrder(PF_Request objPFRequest, VerisignGateway.ProductCode productCode)
        {
            PF_Response objReturn = new PF_Response();

            try
            {
                bool isLinkUsed = BLStoreOrder.CheckConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail!.StoreOrderId);

                if (isLinkUsed)
                {
                    objReturn.ErrorCode = -1000;
                    objReturn.ErrorFiled = "PIN";
                    objReturn.ErrorMessage = "Link is already used.";
                    return objReturn;
                }

                BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, true);

                string orderType = productCode == VerisignGateway.ProductCode.PrimeSSLVerifiedMarkCertificate ? "VMC" : "CMC";
                ProductBase objProd = VerisignUtil.GetGSProductObject(productCode);
                QbV1MarkOrderRequest objGSRequest = BLGlobalsign.GetGlobalSignOrderRequestObject_VMCCMC(objPFRequest, orderType);

                OrderResponse? objGSOrderResponse = null;
                try
                {
                    string jsonRequest = JsonSerializer.Serialize(objGSRequest, new JsonSerializerOptions { WriteIndented = true });
                    LogWriter.LogCARequestResponseObjectToDB(
                        objPFRequest.StoreOrderDetail.Pin,
                        jsonRequest,
                        string.Empty,
                        "GlobalSignVMCRequest");

                    objGSOrderResponse = objProd.QuickGSVMCCMCOrder(objGSRequest);
                }
                catch (Exception ex)
                {
                    LogWriter.LogRequestResponseObjectToDB(objPFRequest, ex);
                    BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, false);
                    throw;
                }

                if (objGSOrderResponse.error != null && objGSOrderResponse.error.ErrorCode < 0)
                {
                    LogWriter.LogCARequestResponseObjectToDB(objPFRequest.StoreOrderDetail.Pin, objGSOrderResponse);
                    LogWriter.LogGlobalSignOrderRequestObjectsToDB(objPFRequest);
                    BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, false);
                    BLGlobalsign.SetErrorFromGlobalSign(objReturn, objGSOrderResponse);
                    return objReturn;
                }

                objReturn.VendorID = objGSOrderResponse.OrderNumber;
                objReturn.ApprovalEmail = objPFRequest.GlobalSignOrderRequest!.ApprovalEmail;

                GSGetSANValidationStatusResponse objResponse = VerisignAPIHelper.GSGetSANValidationStatus(
                    objGSOrderResponse.OrderNumber,
                    objPFRequest.CACredentialDetails!.GetGlobalSignCACredential()!);

                if (objResponse.error.ErrorCode == 0)
                {
                    objReturn.DVC = objResponse.DVC;
                    objReturn.DomainVerificationPage = objResponse.DomainVerificationPage;
                }
                else
                {
                    LogWriter.LogCARequestResponseObjectToDB(
                        objPFRequest.StoreOrderDetail.Pin,
                        objResponse.CARequestObject,
                        JsonSerializer.Serialize(objResponse.error),
                        "GSGetSANValidationStatus");
                }

                objReturn.GSURLMetaTag = objGSOrderResponse.DNSText;
                objReturn.GSURLs = objGSOrderResponse.DNSVerificaitonDomains;
                objReturn.DomainName = objPFRequest.CSRDetailRow?.DomainName;

                BLGlobalsign.SaveGlobslsignVMCCMCOrderInDB(objPFRequest, objGSOrderResponse, objReturn.DVC);
                objReturn.ErrorCode = 0;
            }
            catch (Exception ex)
            {
                LogWriter.LogRequestResponseObjectToDB(objPFRequest, ex);
                throw;
            }

            return objReturn;
        }
    }
}
