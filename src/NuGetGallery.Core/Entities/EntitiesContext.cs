﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.Infrastructure.Annotations;
using System.Threading.Tasks;

namespace NuGetGallery
{
    [DbConfigurationType(typeof(EntitiesConfiguration))]
    public class EntitiesContext
        : ObjectMaterializedInterceptingDbContext, IEntitiesContext
    {
        private const string CertificatesThumbprintIndex = "IX_Certificates_Thumbprint";

        static EntitiesContext()
        {
            // Don't run migrations, ever!
            Database.SetInitializer<EntitiesContext>(null);
        }

        /// <summary>
        /// This constructor is provided mainly for purposes of running migrations from Package Manager console,
        /// or any other scenario where a connection string will be set after the EntitiesContext is created
        /// (and read only mode is don't care).
        /// </summary>
        public EntitiesContext()
            : this("Gallery.SqlServer", false) // Use the connection string in a web.config (if one is found)
        {
        }

        /// <summary>
        /// The NuGet Gallery code should usually use this constructor, in order to respect read only mode.
        /// </summary>
        public EntitiesContext(string connectionString, bool readOnly)
            : base(connectionString)
        {
            ReadOnly = readOnly;
        }

        public bool ReadOnly { get; private set; }
        public IDbSet<CuratedFeed> CuratedFeeds { get; set; }
        public IDbSet<CuratedPackage> CuratedPackages { get; set; }
        public IDbSet<PackageRegistration> PackageRegistrations { get; set; }
        public IDbSet<Credential> Credentials { get; set; }
        public IDbSet<Scope> Scopes { get; set; }
        public IDbSet<UserSecurityPolicy> UserSecurityPolicies { get; set; }
        public IDbSet<ReservedNamespace> ReservedNamespaces { get; set; }
        public IDbSet<Certificate> Certificates { get; set; }

        /// <summary>
        /// User or organization accounts.
        /// </summary>
        public IDbSet<User> Users { get; set; }

        IDbSet<T> IEntitiesContext.Set<T>()
        {
            return base.Set<T>();
        }

        public override async Task<int> SaveChangesAsync()
        {
            if (ReadOnly)
            {
                throw new ReadOnlyModeException("Save changes unavailable: the gallery is currently in read only mode, with limited service. Please try again later.");
            }

            return await base.SaveChangesAsync();
        }

        public void DeleteOnCommit<T>(T entity) where T : class
        {
            Set<T>().Remove(entity);
        }

        public void SetCommandTimeout(int? seconds)
        {
            ObjectContext.CommandTimeout = seconds;
        }

        public Database GetDatabase()
        {
            return Database;
        }

#pragma warning disable 618 // TODO: remove Package.Authors completely once production services definitely no longer need it
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Credential>()
                .HasKey(c => c.Key)
                .HasRequired(c => c.User)
                    .WithMany(u => u.Credentials)
                    .HasForeignKey(c => c.UserKey);

            modelBuilder.Entity<Scope>()
                .HasKey(c => c.Key);

            modelBuilder.Entity<Scope>()
                .HasOptional(sc => sc.Owner)
                .WithMany()
                .HasForeignKey(sc => sc.OwnerKey)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Scope>()
                .HasRequired<Credential>(sc => sc.Credential)
                .WithMany(cr => cr.Scopes)
                .HasForeignKey(sc => sc.CredentialKey)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<PackageLicenseReport>()
                .HasKey(r => r.Key)
                .HasMany(r => r.Licenses)
                .WithMany(l => l.Reports)
                .Map(c => c.ToTable("PackageLicenseReportLicenses")
                           .MapLeftKey("ReportKey")
                           .MapRightKey("LicenseKey"));

            modelBuilder.Entity<PackageLicense>()
                .HasKey(l => l.Key);

            modelBuilder.Entity<User>()
                .HasKey(u => u.Key);

            modelBuilder.Entity<User>()
                .HasMany<EmailMessage>(u => u.Messages)
                .WithRequired(em => em.ToUser)
                .HasForeignKey(em => em.ToUserKey);

            modelBuilder.Entity<User>()
                .HasMany<Role>(u => u.Roles)
                .WithMany(r => r.Users)
                .Map(c => c.ToTable("UserRoles")
                           .MapLeftKey("UserKey")
                           .MapRightKey("RoleKey"));

            modelBuilder.Entity<Organization>()
                .ToTable("Organizations");

            modelBuilder.Entity<Membership>()
                .HasKey(m => new { m.OrganizationKey, m.MemberKey });

            modelBuilder.Entity<User>()
                .HasMany(u => u.Organizations)
                .WithRequired(m => m.Member)
                .HasForeignKey(m => m.MemberKey)
                .WillCascadeOnDelete(true); // Membership will be deleted with the Member account.

            modelBuilder.Entity<Organization>()
                .HasMany(o => o.Members)
                .WithRequired(m => m.Organization)
                .HasForeignKey(m => m.OrganizationKey)
                .WillCascadeOnDelete(true); // Memberships will be deleted with the Organization account.

            modelBuilder.Entity<Role>()
                .HasKey(u => u.Key);

            modelBuilder.Entity<UserSecurityPolicy>()
                .HasRequired<User>(p => p.User)
                .WithMany(cr => cr.SecurityPolicies)
                .HasForeignKey(p => p.UserKey)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<ReservedNamespace>()
                .HasKey(p => p.Key);

            modelBuilder.Entity<ReservedNamespace>()
                .HasMany<PackageRegistration>(rn => rn.PackageRegistrations)
                .WithMany(pr => pr.ReservedNamespaces)
                .Map(prrn => prrn.ToTable("ReservedNamespaceRegistrations")
                                .MapLeftKey("ReservedNamespaceKey")
                                .MapRightKey("PackageRegistrationKey"));

            modelBuilder.Entity<ReservedNamespace>()
                .HasMany<User>(pr => pr.Owners)
                .WithMany(u => u.ReservedNamespaces)
                .Map(c => c.ToTable("ReservedNamespaceOwners")
                           .MapLeftKey("ReservedNamespaceKey")
                           .MapRightKey("UserKey"));

            modelBuilder.Entity<UserSecurityPolicy>()
                .HasKey(p => p.Key);

            modelBuilder.Entity<EmailMessage>()
                .HasKey(em => em.Key);

            modelBuilder.Entity<EmailMessage>()
                .HasOptional<User>(em => em.FromUser)
                .WithMany()
                .HasForeignKey(em => em.FromUserKey);

            modelBuilder.Entity<PackageRegistration>()
                .HasKey(pr => pr.Key);

            modelBuilder.Entity<PackageRegistration>()
                .HasMany<Package>(pr => pr.Packages)
                .WithRequired(p => p.PackageRegistration)
                .HasForeignKey(p => p.PackageRegistrationKey);

            modelBuilder.Entity<PackageRegistration>()
                .HasMany<User>(pr => pr.Owners)
                .WithMany()
                .Map(c => c.ToTable("PackageRegistrationOwners")
                           .MapLeftKey("PackageRegistrationKey")
                           .MapRightKey("UserKey"));

            modelBuilder.Entity<Package>()
                .HasKey(p => p.Key);

            modelBuilder.Entity<Package>()
                .HasMany<PackageAuthor>(p => p.Authors)
                .WithRequired(pa => pa.Package)
                .HasForeignKey(pa => pa.PackageKey);

            modelBuilder.Entity<Package>()
                .HasMany<PackageDependency>(p => p.Dependencies)
                .WithRequired(pd => pd.Package)
                .HasForeignKey(pd => pd.PackageKey);

            modelBuilder.Entity<Package>()
                .HasMany<PackageType>(p => p.PackageTypes)
                .WithRequired(pt => pt.Package)
                .HasForeignKey(pt => pt.PackageKey);

            modelBuilder.Entity<PackageEdit>()
                .HasKey(pm => pm.Key);

            modelBuilder.Entity<PackageEdit>()
                .HasRequired(pm => pm.User)
                .WithMany()
                .HasForeignKey(pm => pm.UserKey)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PackageEdit>()
                .HasRequired<Package>(pm => pm.Package)
                .WithMany(p => p.PackageEdits)
                .HasForeignKey(pm => pm.PackageKey)
                .WillCascadeOnDelete(true); // Pending PackageEdits get deleted with their package, since hey, there's no way to apply them without the package anyway.

            modelBuilder.Entity<PackageHistory>()
                .HasKey(pm => pm.Key);

            modelBuilder.Entity<PackageHistory>()
                .HasOptional(pm => pm.User)
                .WithMany()
                .HasForeignKey(pm => pm.UserKey)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PackageHistory>()
                .HasRequired<Package>(pm => pm.Package)
                .WithMany(p => p.PackageHistories)
                .HasForeignKey(pm => pm.PackageKey)
                .WillCascadeOnDelete(true); // PackageHistories get deleted with their package.

            modelBuilder.Entity<PackageAuthor>()
                .HasKey(pa => pa.Key);

            modelBuilder.Entity<PackageDependency>()
                .HasKey(pd => pd.Key);

            modelBuilder.Entity<GallerySetting>()
                .HasKey(gs => gs.Key);

            modelBuilder.Entity<PackageOwnerRequest>()
                .HasKey(por => por.Key);

            modelBuilder.Entity<PackageFramework>()
                .HasKey(pf => pf.Key);

            modelBuilder.Entity<CuratedFeed>()
                .HasKey(cf => cf.Key);

            modelBuilder.Entity<CuratedFeed>()
                .HasMany<CuratedPackage>(cf => cf.Packages)
                .WithRequired(cp => cp.CuratedFeed)
                .HasForeignKey(cp => cp.CuratedFeedKey);

            modelBuilder.Entity<CuratedFeed>()
                .HasMany<User>(cf => cf.Managers)
                .WithMany()
                .Map(c => c.ToTable("CuratedFeedManagers")
                           .MapLeftKey("CuratedFeedKey")
                           .MapRightKey("UserKey"));

            modelBuilder.Entity<CuratedPackage>()
                .HasKey(cp => cp.Key);

            modelBuilder.Entity<CuratedPackage>()
                .HasRequired(cp => cp.PackageRegistration);

            modelBuilder.Entity<PackageDelete>()
                .HasKey(pd => pd.Key)
                .HasMany(pd => pd.Packages)
                    .WithOptional();

            modelBuilder.Entity<AccountDelete>()
                .HasKey(a => a.Key)
                .HasRequired(a => a.DeletedAccount);

            modelBuilder.Entity<AccountDelete>()
                .HasRequired(a => a.DeletedBy)
                .WithMany()
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Certificate>()
                .HasKey(c => c.Key);

            modelBuilder.Entity<Certificate>()
                .Property(c => c.Thumbprint)
                .HasMaxLength(256)
                .HasColumnType("varchar")
                .IsRequired()
                .HasColumnAnnotation(
                    IndexAnnotation.AnnotationName,
                    new IndexAnnotation(new[]
                    {
                        new IndexAttribute(CertificatesThumbprintIndex)
                        {
                            IsUnique = true,
                        }
                    }));
        }
#pragma warning restore 618
    }
}
