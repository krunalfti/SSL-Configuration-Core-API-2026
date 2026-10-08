using Microsoft.EntityFrameworkCore;

namespace SSLConfiguration.Infrastructure.Persistence
{
    /// <summary>
    /// Thin layer architecture / Core migration: EF Core DbContext using same name as old EF5 context.
    /// </summary>
    public partial class SSLConfigurationEntities : DbContext
    {
        public SSLConfigurationEntities()
        {
        }

        public SSLConfigurationEntities(DbContextOptions<SSLConfigurationEntities> options)
            : base(options)
        {
        }

        public virtual DbSet<ApiClient> ApiClients { get; set; }
        public virtual DbSet<CACredential> CACredentials { get; set; }
        public virtual DbSet<StoreOrder> StoreOrders { get; set; }
        public virtual DbSet<Product> Products { get; set; }
        public virtual DbSet<GlobalSignOrderDetail> GlobalSignOrderDetails { get; set; }
        public virtual DbSet<AdditionalDomain> AdditionalDomains { get; set; }
        public virtual DbSet<CSRDetail> CSRDetails { get; set; }
        public virtual DbSet<GlobalSignContactInfo> GlobalSignContactInfos { get; set; }
        public virtual DbSet<GlobalSignOrganizationInfo> GlobalSignOrganizationInfos { get; set; }
        public virtual DbSet<Country> Countries { get; set; }
        public virtual DbSet<SymantecContactInfo> SymantecContactInfos { get; set; }
        public virtual DbSet<SymantecOrganizationInfo> SymantecOrganizationInfos { get; set; }
        public virtual DbSet<DigicertCertificateDetail> DigicertCertificateDetails { get; set; }
        public virtual DbSet<DigicertAdditionalDomain> DigicertAdditionalDomains { get; set; }
        public virtual DbSet<ChildCertificateDetail> ChildCertificateDetails { get; set; }
        public virtual DbSet<RenewalOrderDetail> RenewalOrderDetails { get; set; }
        public virtual DbSet<StoreMaster> StoreMasters { get; set; }
        public virtual DbSet<ComodoOrderDetail> ComodoOrderDetails { get; set; }
        public virtual DbSet<ComodoContactInfo> ComodoContactInfos { get; set; }
        public virtual DbSet<AcemeCertificateDetail> AcemeCertificateDetails { get; set; }
        public virtual DbSet<AcmeDomainHistory> AcmeDomainHistories { get; set; }
        public virtual DbSet<SectigoRefundPaymentStatu> SectigoRefundPaymentStatus { get; set; }

        public virtual DbSet<AcmeSubcriptonError> AcmeSubcriptonErrors { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                if (string.IsNullOrWhiteSpace(DbConfig.SSLConfigurationEntities))
                {
                    throw new InvalidOperationException(
                        "Connection string 'SSLConfigurationEntities' is not configured. Set it in appsettings.json and call DbConfig.Initialize in Program.cs.");
                }

                optionsBuilder.UseSqlServer(DbConfig.SSLConfigurationEntities);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CACredential>(entity =>
            {
                entity.HasKey(e => e.CACredentialID);
                entity.ToTable("CACredential");
            });

            modelBuilder.Entity<StoreOrder>(entity =>
            {
                entity.HasKey(e => e.StoreOrderId);
                entity.ToTable("StoreOrder");
            });

            modelBuilder.Entity<GlobalSignOrderDetail>(entity =>
            {
                entity.HasKey(e => e.GlobalSignOrderDetailID);
                entity.ToTable("GlobalSignOrderDetail");
            });

            modelBuilder.Entity<AdditionalDomain>(entity =>
            {
                entity.HasKey(e => e.AdditionalDomainId);
                entity.ToTable("AdditionalDomains");
            });

            modelBuilder.Entity<CSRDetail>(entity =>
            {
                entity.HasKey(e => e.CSRDetailId);
                entity.ToTable("CSRDetail");
                entity.Ignore(e => e.PrimaryDomainName);
            });

            modelBuilder.Entity<GlobalSignContactInfo>(entity =>
            {
                entity.HasKey(e => e.GlobalSignContactInfoID);
                entity.ToTable("GlobalSignContactInfo");
            });

            modelBuilder.Entity<GlobalSignOrganizationInfo>(entity =>
            {
                entity.HasKey(e => e.GlobalSignOrganizationInfoID);
                entity.ToTable("GlobalSignOrganizationInfo");
            });

            modelBuilder.Entity<Country>(entity =>
            {
                entity.HasKey(e => e.countryId);
                entity.ToTable("Country");
            });

            modelBuilder.Entity<SymantecContactInfo>(entity =>
            {
                entity.HasKey(e => e.SymantecContactInfoID);
                entity.ToTable("SymantecContactInfo");
            });

            modelBuilder.Entity<SymantecOrganizationInfo>(entity =>
            {
                entity.HasKey(e => e.SymantecOrganizationInfoID);
                entity.ToTable("SymantecOrganizationInfo");
            });

            modelBuilder.Entity<DigicertCertificateDetail>(entity =>
            {
                entity.HasKey(e => e.DigicertCertificateDetailId);
                entity.ToTable("DigicertCertificateDetail");
            });

            modelBuilder.Entity<DigicertAdditionalDomain>(entity =>
            {
                entity.HasKey(e => e.AdditionalDomainId);
                entity.ToTable("DigicertAdditionalDomains");
            });

            modelBuilder.Entity<ChildCertificateDetail>(entity =>
            {
                entity.HasKey(e => e.ChildCertificateDetailId);
                entity.ToTable("ChildCertificateDetail");
            });

            modelBuilder.Entity<RenewalOrderDetail>(entity =>
            {
                entity.HasKey(e => e.RenewalOrderDetailID);
                entity.ToTable("RenewalOrderDetail");
            });

            modelBuilder.Entity<StoreMaster>(entity =>
            {
                entity.HasKey(e => e.StoreMasterId);
                entity.ToTable("StoreMaster");
            });

            modelBuilder.Entity<ComodoOrderDetail>(entity =>
            {
                entity.HasKey(e => e.ComodoOrderDetailId);
                entity.ToTable("ComodoOrderDetail");
            });

            modelBuilder.Entity<ComodoContactInfo>(entity =>
            {
                entity.HasKey(e => e.ComodoContactInfoId);
                entity.ToTable("ComodoContactInfo");
            });

            modelBuilder.Entity<AcemeCertificateDetail>(entity =>
            {
                entity.HasKey(e => e.AcemeDetailsId);
                entity.ToTable("AcemeCertificateDetails");
            });

            modelBuilder.Entity<AcmeDomainHistory>(entity =>
            {
                entity.HasKey(e => e.AcmeDomainHistoryId);
                entity.ToTable("AcmeDomainHistory");
            });
            modelBuilder.Entity<SectigoRefundPaymentStatu>(e =>
            {
                e.ToTable("SectigoRefundPaymentStatus");
                e.HasKey(x => x.SectigoRefundPaymentId);
            });
            modelBuilder.Entity<Product>(e =>
            {
                e.ToTable("Product");
                e.HasKey(x => x.ProductId);
            });
            modelBuilder.Entity<AcmeSubcriptonError>(e =>
            {
                e.ToTable("AcmeSubcriptonError");
                e.HasKey(x => x.AcmeSubcriptonErrorId);
            });
        }
    }
}
