using System.Text.Json;
using SSLConfiguration.Infrastructure;
using SSLConfiguration.Infrastructure.Persistence;
using VerisignGateway;

namespace SSLConfiguration.Application
{
    /// <summary>
    /// Digicert Summary / PlaceOrder — Digicert SSL / CodeSign / VMC / X9 ProductFactory flows.
    /// </summary>
    public static class DigicertPlaceOrder
    {
        public static PF_Response PlaceOrder(PF_Request objPFRequest)
        {
            if (IsCodeSign(objPFRequest))
                return PlaceCodeSignOrder(objPFRequest);

            if (IsVmc(objPFRequest))
                return PlaceVmcOrder(objPFRequest);

            return PlaceSslOrX9Order(objPFRequest);
        }

        public static PF_Response PlaceCodeSignOrder(PF_Request objPFRequest) =>
            ExecutePlaceOrder(objPFRequest, buildRequest: req =>
            {
                DigicertOrderRequest digicertOrderRequest = BLDigicert.GetDigicertOrderRequestObject(req);
                digicertOrderRequest.CsProvisioningMethod = req.DigicertOrderRequest.CodeSignProvisioningMethod;

                if (req.DigicertOrderRequest.EVApproverContactInfo != null
                    && !string.IsNullOrEmpty(req.DigicertOrderRequest.EVApproverContactInfo.Email))
                {
                    digicertOrderRequest.AdditionalEmails = new[] { req.DigicertOrderRequest.EVApproverContactInfo.Email };
                }

                string method = (req.DigicertOrderRequest.CodeSignProvisioningMethod ?? string.Empty).ToLower();

                if (method == "client_app")
                {
                    if (digicertOrderRequest.Certificate == null)
                        digicertOrderRequest.Certificate = new Certificate();
                    if (digicertOrderRequest.Certificate.ServerPlatform == null)
                        digicertOrderRequest.Certificate.ServerPlatform = new Container();

                    digicertOrderRequest.Certificate.ServerPlatform.Id = req.DigicertOrderRequest.ServerHardwarePlatformId;
                    digicertOrderRequest.Certificate.CACertID = req.DigicertOrderRequest.IntermediateCAId;
                }

                if (method == "ship_token")
                {
                    digicertOrderRequest.Certificate.CSR = string.Empty;
                    if (req.DigicertOrderRequest.ShippingInformation != null
                        && !string.IsNullOrEmpty(req.DigicertOrderRequest.ShippingInformation.ShippingAddress1))
                    {
                        digicertOrderRequest.ShipInfo = new ShipInfo
                        {
                            Method = "EXPEDITED",
                            Addr1 = req.DigicertOrderRequest.ShippingInformation.ShippingAddress1,
                            Addr2 = req.DigicertOrderRequest.ShippingInformation.ShippingAddress2,
                            City = req.DigicertOrderRequest.ShippingInformation.ShippingCity,
                            Country = req.DigicertOrderRequest.ShippingInformation.ShippingCountryName,
                            Name = req.DigicertOrderRequest.ShippingInformation.ShippingName,
                            State = req.DigicertOrderRequest.ShippingInformation.ShippingState,
                            Zip = req.DigicertOrderRequest.ShippingInformation.ShippingPostalCode
                        };
                    }
                }

                return digicertOrderRequest;
            }, afterSave: (req, digicertOrderRequest) =>
            {
                try
                {
                    if (digicertOrderRequest.Organization == null || digicertOrderRequest.DigicertCredential == null)
                        return;

                    var evApproverContactInfo = new OrganizationContact
                    {
                        Email = req.DigicertOrderRequest.EVApproverContactInfo?.Email,
                        FirstName = req.DigicertOrderRequest.EVApproverContactInfo?.FirstName,
                        JobTitle = req.DigicertOrderRequest.EVApproverContactInfo?.Title,
                        LastName = req.DigicertOrderRequest.EVApproverContactInfo?.LastName,
                        Telephone = req.DigicertOrderRequest.EVApproverContactInfo?.PhoneNo
                    };

                    DigicertAPIHelper.SubmitOrganizationForValidation(
                        digicertOrderRequest.Organization.Id,
                        evApproverContactInfo,
                        new List<string> { DigicertOVValidationType.OV, DigicertOVValidationType.CS },
                        digicertOrderRequest.DigicertCredential);
                }
                catch
                {
                }
            });

        public static PF_Response PlaceVmcOrder(PF_Request objPFRequest) =>
            ExecutePlaceOrder(objPFRequest, buildRequest: BLDigicert.GetDigicertOrderRequestObject_VMC, afterSave: (req, digicertOrderRequest) =>
            {
                try
                {
                    if (digicertOrderRequest.Organization == null || digicertOrderRequest.DigicertCredential == null)
                        return;

                    var evApproverContactInfo = new OrganizationContact
                    {
                        Email = req.DigicertOrderRequest.OrganizationContactInfo?.Email,
                        FirstName = req.DigicertOrderRequest.OrganizationContactInfo?.FirstName,
                        JobTitle = req.DigicertOrderRequest.OrganizationContactInfo?.Title,
                        LastName = req.DigicertOrderRequest.OrganizationContactInfo?.LastName,
                        Telephone = req.DigicertOrderRequest.OrganizationContactInfo?.PhoneNo
                    };

                    DigicertAPIHelper.SubmitOrganizationForValidation(
                        digicertOrderRequest.Organization.Id,
                        evApproverContactInfo,
                        new List<string> { DigicertOVValidationType.OV, DigicertOVValidationType.EV },
                        digicertOrderRequest.DigicertCredential);
                }
                catch
                {
                }
            });

        private static PF_Response PlaceSslOrX9Order(PF_Request objPFRequest) =>
            ExecutePlaceOrder(objPFRequest, buildRequest: req =>
            {
                DigicertOrderRequest digicertOrderRequest = BLDigicert.GetDigicertOrderRequestObject(req);

                // Same as Digicert_X9PKI — additional domain approval emails when DCV is EMAIL
                if ((Convert.ToBoolean(req.StoreOrderDetail?.IsX9) || req.ProductDetail?.IsX9 == true)
                    && req.DigicertOrderRequest.ApprovalMethod == (int)DigicertDCVMethod.Email
                    && req.DigicertOrderRequest.AdditionalDomainList != null
                    && req.DigicertOrderRequest.AdditionalDomainList.Count > 0)
                {
                    var dcvEmailList = new List<DcvEmail>();
                    foreach (var addDomain in req.DigicertOrderRequest.AdditionalDomainList)
                    {
                        dcvEmailList.Add(new DcvEmail
                        {
                            DnsName = addDomain.Key,
                            Email = addDomain.Value,
                            EmailDomain = addDomain.Value.GetDomain()
                        });
                    }
                    digicertOrderRequest.DcvEmails = dcvEmailList.ToArray();
                }

                return digicertOrderRequest;
            }, afterSave: (req, digicertOrderRequest) =>
            {
                if (!Convert.ToBoolean(req.StoreOrderDetail?.IsX9) && req.ProductDetail?.IsX9 != true)
                    return;

                try
                {
                    if (digicertOrderRequest.Organization == null || digicertOrderRequest.DigicertCredential == null)
                        return;

                    DigicertAPIHelper.SubmitOrganizationForValidation(
                        digicertOrderRequest.Organization.Id,
                        new OrganizationContact(),
                        new List<string> { DigicertOVValidationType.OV },
                        digicertOrderRequest.DigicertCredential);
                }
                catch (Exception ex)
                {
                    LogWriter.LogRequestResponseObjectToDB(req, ex);
                }
            });

        private static PF_Response ExecutePlaceOrder(
            PF_Request objPFRequest,
            Func<PF_Request, DigicertOrderRequest> buildRequest,
            Action<PF_Request, DigicertOrderRequest>? afterSave = null)
        {
            PF_Response objPFResponse = new PF_Response();

            try
            {
                bool isLinkUsed = BLStoreOrder.CheckConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail!.StoreOrderId);
                if (isLinkUsed)
                {
                    objPFResponse.ErrorCode = -1000;
                    objPFResponse.ErrorFiled = "PIN";
                    objPFResponse.ErrorMessage = "Link is already used.";
                    return objPFResponse;
                }

                BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, true);

                ProductBase objProd = VerisignUtil.GetProductObject((VerisignGateway.ProductCode)objPFRequest.StoreOrderDetail.ProductId);
                DigicertOrderRequest digicertOrderRequest = buildRequest(objPFRequest);
                OrderResponse? objDigicertOrderResponse = null;

                try
                {
                    LogWriter.LogDigicertRequestObjects(
                        objPFRequest.StoreOrderDetail.SSLApiLinkId,
                        objPFRequest.StoreOrderDetail.Pin,
                        JsonSerializer.Serialize(digicertOrderRequest));

                    objDigicertOrderResponse = objProd.QuickDigicertOrder(digicertOrderRequest);
                }
                catch (Exception ex)
                {
                    LogWriter.LogRequestResponseObjectToDB(objPFRequest, ex);
                    BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, false);
                    throw;
                }

                if (objDigicertOrderResponse == null)
                {
                    BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, false);
                    objPFResponse.ErrorCode = -9999;
                    objPFResponse.ErrorMessage = "No response received from DigiCert.";
                    return objPFResponse;
                }

                if (objDigicertOrderResponse.DigicertAPIErrors.StatusCode < 0)
                {
                    LogWriter.LogCARequestResponseObjectToDB(objPFRequest.StoreOrderDetail.Pin, objDigicertOrderResponse);
                    LogWriter.LogDigicertOrderRequestObjectsToDB(objPFRequest);
                    BLStoreOrder.UpdateConfigurationLinkUsedStatus(objPFRequest.StoreOrderDetail.StoreOrderId, false);
                    BLDigicert.SetErrorFromDigicert(objPFResponse, objDigicertOrderResponse.DigicertAPIErrors);
                    return objPFResponse;
                }

                objPFResponse.DigicertOrderNumber = objDigicertOrderResponse.DigicertOrderNumber;
                objPFResponse.DigicertCertificateId = objDigicertOrderResponse.DigicertCertificateId;
                objPFResponse.DigicertDCVRandomValue = objDigicertOrderResponse.DigicertDCVRandomValue;
                objPFResponse.ApprovalEmail = objPFRequest.DigicertOrderRequest.ApproverEmail;
                objPFResponse.DomainName = objPFRequest.CSRDetailRow?.DomainName;

                BLDigicert.SaveDigicertOrderInDB(objPFRequest, objDigicertOrderResponse);
                afterSave?.Invoke(objPFRequest, digicertOrderRequest);
            }
            catch (Exception ex)
            {
                LogWriter.LogRequestResponseObjectToDB(objPFRequest, ex);
                throw;
            }

            return objPFResponse;
        }

        private static bool IsCodeSign(PF_Request req) =>
            Convert.ToBoolean(req.StoreOrderDetail?.IsCodeSign)
            || Convert.ToBoolean(req.ProductDetail?.IsCodeSign)
            || string.Equals(req.ProductDetail?.AuthenticationType, ConstantUtil.AuthenticationType_CodeSign, StringComparison.OrdinalIgnoreCase);

        private static bool IsVmc(PF_Request req) =>
            string.Equals(req.ProductDetail?.AuthenticationType, ConstantUtil.AuthenticationType_VMC, StringComparison.OrdinalIgnoreCase)
            || string.Equals(req.StoreOrderDetail?.AuthenticationType, ConstantUtil.AuthenticationType_VMC, StringComparison.OrdinalIgnoreCase);
    }
}
