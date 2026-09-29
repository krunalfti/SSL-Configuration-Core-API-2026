using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using SSLConfiguration.Infrastructure;
using SSLConfiguration.Infrastructure.DataAccess;
using SSLConfiguration.Infrastructure.Persistence;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using VerisignGateway;

namespace SSLConfiguration.Application
{
    /// <summary>
    /// Thin layer architecture / Core migration: keep same BLGeneral method names as old repo.
    /// </summary>
    public class BLGeneral
    {
        /// <summary>
        /// Same method as old BLGeneral.GetCACredentials(string) — via CACredentialDataAccess.GetByCredentialCode.
        /// </summary>
        public static CACredential? GetCACredentials(string caCredentialCode)
        {
            return CACredentialDataAccess.GetByCredentialCode(caCredentialCode);
        }

        /// <summary>
        /// Same method as old BLGeneral.GetCACredentials(int) — uses SP GetCACredentialCode via CACredentialDataAccess.
        /// </summary>
        public static CACredential? GetCACredentials(int StoreOrderId)
        {
            return CACredentialDataAccess.GetByStoreOrderId(StoreOrderId);
        }

        /// <summary>
        /// Same method as old BLGeneral.IsVMCProduct — uses VerisignGateway.ProductCode values.
        /// </summary>
        public static bool IsVMCProduct(int productId)
        {
            if (productId == (int)VerisignGateway.ProductCode.DigicertVerifiedMarkCertificate
                || productId == (int)VerisignGateway.ProductCode.DigicertCommonMarkCertificate)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Same method as old BLGeneral.IsPrimeVMCProduct — uses VerisignGateway.ProductCode values.
        /// </summary>
        public static bool IsPrimeVMCProduct(int productId)
        {
            if (productId == (int)VerisignGateway.ProductCode.PrimeSSLVerifiedMarkCertificate
                || productId == (int)VerisignGateway.ProductCode.PrimeSSLCommonMarkCertificate)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Same method as old BLGeneral.GetProductDetail_StoreOrder.
        /// </summary>
        public static Product? GetProductDetail_StoreOrder(string pin)
        {
            Product objProduct;

            StoreOrder? storeOrder = StoreOrderDataAccess.GetByPin(pin);

            if (storeOrder != null)
            {
                objProduct = new Product()
                {
                    ProductId = storeOrder.ProductId,
                    ProductName = storeOrder.ProductName,
                    BrandName = storeOrder.ProductName,
                    IsWildcard = Convert.ToBoolean(storeOrder.IsWildcard),
                    AuthenticationType = storeOrder.AuthenticationType,
                    IsMultiDomain = Convert.ToBoolean(storeOrder.IsMultiDomain),
                    IsWildcardMultiDomain = Convert.ToBoolean(storeOrder.IsWildcardMultiDomain),
                    IsFlex = Convert.ToBoolean(storeOrder.IsFlex),
                    IsCodeSign = Convert.ToBoolean(storeOrder.IsCodeSign),
                    IsPAC = Convert.ToBoolean(storeOrder.IsPAC),
                    IsX9 = storeOrder.IsX9
                };
                return objProduct;
            }

            return null;
        }

        /// <summary>
        /// Same method as old BLGeneral.PasreCSR (typo kept on purpose) — BouncyCastle parse, returns VerisignGateway.ValidateAndParseCSRResponse.
        /// </summary>
        public static ValidateAndParseCSRResponse PasreCSR(string csr)
        {
            try
            {
                char[] characters = csr.Replace("-----BEGIN CERTIFICATE REQUEST-----", "")
                                    .Replace("-----END CERTIFICATE REQUEST-----", "")
                                    .Replace("-----BEGIN NEW CERTIFICATE REQUEST-----", "")
                                    .Replace("-----END NEW CERTIFICATE REQUEST-----", "")
                                    .ToCharArray();

                byte[] csrEncode = Convert.FromBase64CharArray(characters, 0, characters.Length);
                Pkcs10CertificationRequest decodedCsr = new Pkcs10CertificationRequest(csrEncode);

                CertificationRequestInfo certificateRequestInfo = decodedCsr.GetCertificationRequestInfo();

                X509Name subject = certificateRequestInfo.Subject;

                ValidateAndParseCSRResponse validateAndParseCSR = new ValidateAndParseCSRResponse()
                {
                    Country = subject.GetValueList(X509Name.C).OfType<string>().FirstOrDefault(),
                    DomainName = subject.GetValueList(X509Name.CN).OfType<string>().FirstOrDefault(),
                    Locality = subject.GetValueList(X509Name.L).OfType<string>().FirstOrDefault(),
                    Organisation = subject.GetValueList(X509Name.O).OfType<string>().FirstOrDefault(),
                    OrganisationUnit = subject.GetValueList(X509Name.OU).OfType<string>().FirstOrDefault(),
                    State = subject.GetValueList(X509Name.ST).OfType<string>().FirstOrDefault(),
                    Email = subject.GetValueList(X509Name.EmailAddress).OfType<string>().FirstOrDefault(),
                };

                try
                {
                    if (certificateRequestInfo.Attributes != null && certificateRequestInfo.Attributes.Count > 0)
                    {
                        DerSequence extensionSequence = certificateRequestInfo.Attributes.OfType<DerSequence>()
                                                                              .First(o => o.OfType<DerObjectIdentifier>()
                                                                                           .Any(oo => oo.Id == "1.2.840.113549.1.9.14"));

                        DerSet extensionSet = extensionSequence.OfType<DerSet>().First();

                        DerOctetString sanObjects = GetAsn1ObjectRecursive<DerOctetString>(extensionSet.OfType<DerSequence>().First(), "2.5.29.17");

                        if (null != sanObjects)
                        {
                            GeneralNames sanNames = GeneralNames.GetInstance(Asn1Object.FromByteArray(sanObjects.GetOctets()));

                            string sanList = string.Empty;
                            foreach (var san in sanNames.GetNames())
                            {
                                sanList += san.Name + ",";
                            }

                            validateAndParseCSR.SAN = sanList.Trim(',');
                        }
                    }

                    SubjectPublicKeyInfo pubKeyInfo = certificateRequestInfo.SubjectPublicKeyInfo;
                    if (!pubKeyInfo.Algorithm.Algorithm.Id.Equals("1.2.840.10045.2.1")
                        && !pubKeyInfo.Algorithm.Algorithm.Id.Equals("1.3.36.3.3.2.8.1.1.11"))
                    {
                        RsaKeyParameters keyParams = (RsaKeyParameters)PublicKeyFactory.CreateKey(pubKeyInfo);
                        validateAndParseCSR.KeySize = Convert.ToString(keyParams.Modulus.BitLength);
                    }
                }
                catch { }

                validateAndParseCSR.error = new APIError()
                {
                    ErrorCode = 0,
                    ErrorField = string.Empty,
                    ErrorMessage = string.Empty
                };

                return validateAndParseCSR;
            }
            catch (Exception ex)
            {
                LogWriter.LogErrorDetails(ex);
                ValidateAndParseCSRResponse validateAndParseCSR = new ValidateAndParseCSRResponse()
                {
                    error = new APIError()
                    {
                        ErrorCode = -1000,
                        ErrorField = "Validate and Parse CSR",
                        ErrorMessage = "Error while parsing csr. Please contact support."
                    }
                };

                return validateAndParseCSR;
            }
        }

        static T GetAsn1ObjectRecursive<T>(DerSequence sequence, string id) where T : Asn1Object
        {
            if (sequence.OfType<DerObjectIdentifier>().Any(o => o.Id == id))
            {
                return sequence.OfType<T>().First();
            }

            foreach (DerSequence subSequence in sequence.OfType<DerSequence>())
            {
                T value = GetAsn1ObjectRecursive<T>(subSequence, id);
                if (value != default(T))
                {
                    return value;
                }
            }

            return default(T)!;
        }

        /// <summary>
        /// Same as old BLGeneral.GetDefaultComodoCredential.
        /// </summary>
        public static ComodoCACredential? GetDefaultComodoCredential()
        {
            string? ComodoCredentialCode = AppConfig.DefaultComodoCredential;
            if (string.IsNullOrWhiteSpace(ComodoCredentialCode))
            {
                return null;
            }

            CACredential? caCredential = GetCACredentials(ComodoCredentialCode);
            if (caCredential == null)
            {
                return null;
            }

            return caCredential.GetComodoCACredential();
        }

        /// <summary>
        /// Same as old BLGeneral.GetComodoApprovalEmailList — uses VerisignAPIHelper.GetComodoApproveremail.
        /// </summary>
        public static List<SelectListItem> GetComodoApprovalEmailList(string domainName, ComodoCACredential? objComodoCACredential, string? selectedValue = null)
        {
            try
            {
                List<SelectListItem> approverEmailList = new List<SelectListItem>();
                approverEmailList.Add(new SelectListItem { Value = "", Text = "--Select--" });

                if (objComodoCACredential == null)
                {
                    return approverEmailList;
                }

                ApproverEmailListResponse objResponse = VerisignAPIHelper.GetComodoApproveremail(domainName, objComodoCACredential);

                if (objResponse.error.ErrorCode == 0)
                {
                    string[] EmailList = objResponse.ApproverEmailList;
                    EmailList = EmailList.OrderBy(d => d).ToArray();

                    foreach (string mailList in EmailList)
                    {
                        approverEmailList.Add(new SelectListItem { Value = mailList, Text = mailList, Selected = selectedValue == mailList });
                    }
                }
                return approverEmailList;
            }
            catch (Exception)
            {
                return new List<SelectListItem>();
            }
        }

        /// <summary>
        /// Same as old BLGeneral.GetAddDomain.
        /// </summary>
        public static int GetAddDomain(int storeOrderId, int productId)
        {
            try
            {
                int san = 0;
                int minDom = 0;
                int addDomain = 0;

                StoreOrder? sslApiLink = StoreOrderDataAccess.GetById(storeOrderId);

                if (sslApiLink != null)
                {
                    san = Convert.ToInt32(sslApiLink.San);
                    minDom = Convert.ToInt32(sslApiLink.MinSAN);

                    addDomain = san + minDom;
                }

                return addDomain;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Same as old BLGeneral.GetWildcardSANCount.
        /// </summary>
        public static int GetWildcardSANCount(int storeOrderId, int productId)
        {
            try
            {
                StoreOrder? sslApiLink = StoreOrderDataAccess.GetById(storeOrderId);

                if (sslApiLink != null)
                {
                    return Convert.ToInt32(sslApiLink.WildcardSAN);
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Same as old BLGeneral.GetTotalAdditionalSANForSymantec.
        /// </summary>
        public static int GetTotalAdditionalSANForSymantec(string Domain, Dictionary<string, string>? SANList)
        {
            int intReturn = 0;

            if (SANList != null)
            {
                foreach (string strSAN in SANList.Keys)
                {
                    if (Domain.ToLower() != strSAN.ToLower())
                        intReturn++;
                }
            }

            return intReturn;
        }

        /// <summary>
        /// Same as old BLGeneral.GetTotalAdditionalSANForComodo.
        /// </summary>
        public static int GetTotalAdditionalSANForComodo(string Domain, Dictionary<string, string>? SANList)
            => GetTotalAdditionalSANForSymantec(Domain, SANList);

        /// <summary>
        /// Same as old BLGeneral.GetApprovalEmailList (GlobalSign).
        /// </summary>
        public static List<SelectListItem> GetApprovalEmailList(string domainName, int productId, GlobalsignCACredential? objGSCACredential)
        {
            try
            {
                List<SelectListItem> approverEmailList = new List<SelectListItem>();
                approverEmailList.Add(new SelectListItem { Value = "", Text = "--Select--" });

                if (objGSCACredential == null)
                    return approverEmailList;

                VerisignGateway.ProductCode pcode = GetProductCodeByProductName(productId); //Get Product Code
                GSApproverEmailListResponse gsapprovalmail = VerisignAPIHelper.GetGSApproverEmailList(domainName, pcode, objGSCACredential);

                if (gsapprovalmail.error.ErrorCode == 0)
                {
                    string[] EmailList = gsapprovalmail.ApproverEmailList;
                    EmailList = EmailList.OrderBy(d => d).ToArray();

                    foreach (string mailList in EmailList)
                    {
                        approverEmailList.Add(new SelectListItem { Value = mailList, Text = mailList });
                    }
                }

                return approverEmailList;
            }
            catch (Exception)
            {
                return new List<SelectListItem>();
            }
        }

        /// <summary>
        /// Same as old BLGeneral.GetDomainTypeList.
        /// </summary>
        public static List<SelectListItem> GetDomainTypeList(int productId)
        {
            try
            {
                List<SelectListItem> domainTypeList = new List<SelectListItem>();
                domainTypeList.Add(new SelectListItem { Value = "", Text = "--Select--" });

                VerisignGateway.ProductCode pcode = GetProductCodeByProductName(productId);
                GSProductType objgstype = VerisignAPIHelper.GetGSProductType(pcode);
                if (GSProductType.DomainVetted == objgstype)
                {
                    string[] SANtype = { "SubDomain" };

                    foreach (string item in SANtype)
                    {
                        domainTypeList.Add(new SelectListItem { Value = item, Text = item });
                    }
                }
                else
                {
                    string[] SANtype = { "FQDN", "SubDomain" };

                    foreach (string item in SANtype)
                    {
                        domainTypeList.Add(new SelectListItem { Value = item, Text = item });
                    }
                }

                return domainTypeList;
            }
            catch (Exception)
            {
                return new List<SelectListItem>();
            }
        }

        /// <summary>
        /// Same as old BLGeneral.GetGlobalSignApprovalMethods.
        /// </summary>
        public static List<SelectListItem> GetGlobalSignApprovalMethods(bool isWildcard = false)
        {
            List<SelectListItem> approvalMethods = new List<SelectListItem>();
            approvalMethods.Add(new SelectListItem { Value = "", Text = "--Select--" });
            if (!isWildcard)
            {
                approvalMethods.Add(new SelectListItem { Value = "1", Text = "Url" });
            }
            approvalMethods.Add(new SelectListItem { Value = "2", Text = "Email" });
            approvalMethods.Add(new SelectListItem { Value = "3", Text = "DNS" });

            return approvalMethods;
        }

        /// <summary>
        /// Same as old BLGeneral.GlobalSignDeleteExistingRecordsForIssueCertificate.
        /// </summary>
        public static bool GlobalSignDeleteExistingRecordsForIssueCertificate(PF_Request objPFRequest)
        {
            if (objPFRequest != null && objPFRequest.StoreOrderDetail != null)
            {
                var storeOrderId = new Microsoft.Data.SqlClient.SqlParameter("@StoreOrderId", System.Data.SqlDbType.BigInt)
                {
                    Value = objPFRequest.StoreOrderDetail.StoreOrderId
                };
                var apiOrderNo = new Microsoft.Data.SqlClient.SqlParameter("@ApiOrderNo", System.Data.SqlDbType.NVarChar)
                {
                    Value = (object?)objPFRequest.StoreOrderDetail.ApiOrderNo ?? DBNull.Value
                };

                using (var dbContext = new SSLConfigurationEntities())
                {
                    var result = dbContext.Database
                        .SqlQueryRaw<AdminCAOrderValidityResponseModel>(
                            "EXEC GlobalSignDeleteExistingRecordsForIssueCertificate @StoreOrderId, @ApiOrderNo",
                            storeOrderId, apiOrderNo)
                        .AsEnumerable()
                        .SingleOrDefault();

                    if (result != null && result.ResultCode == 1)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Same as old BLGeneral.GetProductCodeByProductName.
        /// </summary>
        public static VerisignGateway.ProductCode GetProductCodeByProductName(int PId)
        {
            VerisignGateway.ProductCode pcode = 0;
            if (Enum.IsDefined(typeof(VerisignGateway.ProductCode), PId))
                pcode = (VerisignGateway.ProductCode)PId;
            return pcode;
        }

        /// <summary>
        /// Same as old BLGeneral.isValidSingleOrWildcardDomain.
        /// </summary>
        public static bool isValidSingleOrWildcardDomain(string domainName)
        {
            string strRegex = @"^(\*\.|)((?!-)[A-Za-z0-9-]{1,63}(?<!-)\.)+[A-Za-z0-9-]{2,63}$";
            Regex re = new Regex(strRegex);

            if (re.IsMatch(domainName))
                return true;

            IPAddress? ipAddress;
            IPAddress.TryParse(domainName, out ipAddress);
            return ipAddress != null;
        }

        /// <summary>
        /// Same as old BLGeneral.isValidSingleDomain.
        /// </summary>
        public static bool isValidSingleDomain(string domainName)
        {
            string strRegex = @"^((?!-)[A-Za-z0-9-]{1,63}(?<!-)\.)+[A-Za-z0-9-]{2,63}$";
            Regex re = new Regex(strRegex);

            if (re.IsMatch(domainName))
                return true;
            else
            {
                IPAddress? ipAddress;
                IPAddress.TryParse(domainName, out ipAddress);

                if (ipAddress != null)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Same as old BLGeneral.isValidWildcardDomain.
        /// </summary>
        public static bool isValidWildcardDomain(string domainName)
        {
            string strRegex = @"^(\*\.)((?!-)[A-Za-z0-9-]{1,63}(?<!-)\.)+[A-Za-z0-9-]{2,63}$";
            Regex re = new Regex(strRegex);

            if (re.IsMatch(domainName))
                return true;
            else
                return false;
        }

        /// <summary>
        /// Same as old BLGeneral.GetCountryList.
        /// </summary>
        public static List<SelectListItem> GetCountryList()
        {
            List<SelectListItem> countryList = new List<SelectListItem>();
            countryList.Add(new SelectListItem { Value = "", Text = "--Select--" });

            try
            {
                var countryListResult = CountryDataAccess.GetAll();
                foreach (var country in countryListResult)
                {
                    countryList.Add(new SelectListItem { Value = country.countryCode, Text = country.countryName });
                }
            }
            catch
            {
            }

            return countryList;
        }

        /// <summary>
        /// Same as old BLGeneral.GetCodeSingHSMList.
        /// </summary>
        public static List<SelectListItem> GetCodeSingHSMList()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- Select --" },
                new SelectListItem { Value = "LUNA", Text = "LUNA" },
                new SelectListItem { Value = "YUBIKEY", Text = "YUBIKEY" },
                new SelectListItem { Value = "MARVELL_GOOGLE", Text = "GOOGLE CLOUD" }
            };
        }

        /// <summary>
        /// Same as old BLGeneral.IsOVEVProduct_StoreOrder.
        /// </summary>
        public static bool IsOVEVProduct_StoreOrder(int storeOrderId)
        {
            try
            {
                StoreOrder? storeOrder = StoreOrderDataAccess.GetById(storeOrderId);

                if (storeOrder != null)
                {
                    if (storeOrder.AuthenticationType == ConstantUtil.AuthenticationType_OV || storeOrder.AuthenticationType == ConstantUtil.AuthenticationType_EV)
                        return true;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Same as old BLGeneral.IsEVProduct_StoreOrder.
        /// </summary>
        public static bool IsEVProduct_StoreOrder(int storeOrderId)
        {
            try
            {
                StoreOrder? storeOrder = StoreOrderDataAccess.GetById(storeOrderId);
                return storeOrder != null
                    && storeOrder.AuthenticationType == ConstantUtil.AuthenticationType_EV;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Same as old BLGeneral.IsCodeSignProduct_StoreOrder.
        /// </summary>
        public static bool IsCodeSignProduct_StoreOrder(int storeOrderId)
        {
            try
            {
                StoreOrder? storeOrder = StoreOrderDataAccess.GetById(storeOrderId);
                return storeOrder != null && Convert.ToBoolean(storeOrder.IsCodeSign);
            }
            catch
            {
                return false;
            }
        }
       
    }

    /// <summary>
    /// Same as old BLGeneral.MyExtendedMethods (AddUniqueKey).
    /// </summary>
    public static class MyExtendedMethods
    {
        public static void AddUniqueKey(this Dictionary<string, string> myDict, string key, string value)
        {
            if (myDict.ContainsKey(key) == false)
                myDict.Add(key, value);
        }

        public static void AddUniqueKey(this List<string> myDict, string key)
        {
            if (myDict != null && myDict.Contains(key) == false)
                myDict.Add(key);
        }
    }
}
