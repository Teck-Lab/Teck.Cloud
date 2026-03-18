CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

CREATE TABLE "Tenants" (
    "Id" uuid NOT NULL,
    "Identifier" character varying(100) NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Plan" character varying(50) NOT NULL,
    "KeycloakOrganizationId" character varying(64),
    "DatabaseStrategy" character varying(50) NOT NULL,
    "DatabaseProvider" character varying(50) NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" character varying(100),
    "UpdatedOn" timestamp with time zone,
    "UpdatedBy" character varying(100),
    "DeletedOn" timestamp with time zone,
    "DeletedBy" character varying(100),
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_Tenants" PRIMARY KEY ("Id")
);

CREATE TABLE "TenantDatabaseMetadata" (
    "TenantId" uuid NOT NULL,
    "ServiceName" character varying(100) NOT NULL,
    "WriteEnvVarKey" character varying(500) NOT NULL,
    "ReadEnvVarKey" character varying(500),
    "ReadDatabaseMode" integer NOT NULL,
    "Id" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" text,
    "UpdatedOn" timestamp with time zone,
    "UpdatedBy" text,
    "DeletedOn" timestamp with time zone,
    "DeletedBy" text,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_TenantDatabaseMetadata" PRIMARY KEY ("TenantId", "ServiceName"),
    CONSTRAINT "FK_TenantDatabaseMetadata_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_Tenants_Identifier" ON "Tenants" ("Identifier");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260303203219_InitialPostgreSQL', '10.0.2');

