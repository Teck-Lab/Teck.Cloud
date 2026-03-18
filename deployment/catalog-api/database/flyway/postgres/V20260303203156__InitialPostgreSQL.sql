CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

CREATE TABLE "Brands" (
    "Id" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Description" character varying(1000),
    "Website" character varying(2048),
    "TenantId" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" character varying(100),
    "UpdatedOn" timestamp with time zone,
    "UpdatedBy" character varying(100),
    "DeletedOn" timestamp with time zone,
    "DeletedBy" character varying(100),
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_Brands" PRIMARY KEY ("Id")
);

CREATE TABLE "Categories" (
    "Id" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Description" character varying(1000),
    "TenantId" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" character varying(100),
    "UpdatedOn" timestamp with time zone,
    "UpdatedBy" character varying(100),
    "DeletedOn" timestamp with time zone,
    "DeletedBy" character varying(100),
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_Categories" PRIMARY KEY ("Id")
);

CREATE TABLE "ProductPriceTypes" (
    "Id" uuid NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Priority" integer NOT NULL,
    "TenantId" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" character varying(100),
    "UpdatedOn" timestamp with time zone,
    "UpdatedBy" character varying(100),
    "DeletedOn" timestamp with time zone,
    "DeletedBy" character varying(100),
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_ProductPriceTypes" PRIMARY KEY ("Id")
);

CREATE TABLE "Promotions" (
    "Id" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Description" character varying(1000),
    "ValidFrom" timestamp with time zone NOT NULL,
    "ValidTo" timestamp with time zone NOT NULL,
    "TenantId" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" character varying(100),
    "UpdatedOn" timestamp with time zone,
    "UpdatedBy" character varying(100),
    "DeletedOn" timestamp with time zone,
    "DeletedBy" character varying(100),
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_Promotions" PRIMARY KEY ("Id")
);

CREATE TABLE "Suppliers" (
    "Id" uuid NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(500),
    "Website" character varying(255),
    "TenantId" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" character varying(100),
    "UpdatedOn" timestamp with time zone,
    "UpdatedBy" character varying(100),
    "DeletedOn" timestamp with time zone,
    "DeletedBy" character varying(100),
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_Suppliers" PRIMARY KEY ("Id")
);

CREATE TABLE "Products" (
    "Id" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Description" character varying(2000),
    "Slug" character varying(250) NOT NULL,
    "IsActive" boolean NOT NULL DEFAULT TRUE,
    "SKU" character varying(100) NOT NULL,
    "GTIN" character varying(14),
    "BrandId" uuid,
    "TenantId" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" character varying(100),
    "UpdatedOn" timestamp with time zone,
    "UpdatedBy" character varying(100),
    "DeletedOn" timestamp with time zone,
    "DeletedBy" character varying(100),
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_Products" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Products_Brands_BrandId" FOREIGN KEY ("BrandId") REFERENCES "Brands" ("Id") ON DELETE SET NULL
);

CREATE TABLE "PromotionCategories" (
    "CategoryId" uuid NOT NULL,
    "PromotionId" uuid NOT NULL,
    CONSTRAINT "PK_PromotionCategories" PRIMARY KEY ("CategoryId", "PromotionId"),
    CONSTRAINT "FK_PromotionCategories_Categories_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES "Categories" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_PromotionCategories_Promotions_PromotionId" FOREIGN KEY ("PromotionId") REFERENCES "Promotions" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ProductCategories" (
    "CategoryId" uuid NOT NULL,
    "ProductId" uuid NOT NULL,
    CONSTRAINT "PK_ProductCategories" PRIMARY KEY ("CategoryId", "ProductId"),
    CONSTRAINT "FK_ProductCategories_Categories_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES "Categories" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ProductCategories_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ProductPrices" (
    "Id" uuid NOT NULL,
    "ProductId" uuid,
    "SalePrice" numeric(18,2) NOT NULL,
    "CurrencyCode" character varying(3) NOT NULL,
    "ProductPriceTypeId" uuid,
    "ProductPriceTypeId1" uuid,
    "TenantId" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" character varying(100),
    "UpdatedOn" timestamp with time zone,
    "UpdatedBy" character varying(100),
    "DeletedOn" timestamp with time zone,
    "DeletedBy" character varying(100),
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_ProductPrices" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ProductPrices_ProductPriceTypes_ProductPriceTypeId" FOREIGN KEY ("ProductPriceTypeId") REFERENCES "ProductPriceTypes" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ProductPrices_ProductPriceTypes_ProductPriceTypeId1" FOREIGN KEY ("ProductPriceTypeId1") REFERENCES "ProductPriceTypes" ("Id"),
    CONSTRAINT "FK_ProductPrices_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ProductPromotions" (
    "ProductId" uuid NOT NULL,
    "PromotionId" uuid NOT NULL,
    CONSTRAINT "PK_ProductPromotions" PRIMARY KEY ("ProductId", "PromotionId"),
    CONSTRAINT "FK_ProductPromotions_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ProductPromotions_Promotions_PromotionId" FOREIGN KEY ("PromotionId") REFERENCES "Promotions" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_ProductCategories_ProductId" ON "ProductCategories" ("ProductId");

CREATE UNIQUE INDEX "IX_ProductPriceTypes_Name" ON "ProductPriceTypes" ("Name");

CREATE INDEX "IX_ProductPrices_ProductId" ON "ProductPrices" ("ProductId");

CREATE INDEX "IX_ProductPrices_ProductPriceTypeId" ON "ProductPrices" ("ProductPriceTypeId");

CREATE INDEX "IX_ProductPrices_ProductPriceTypeId1" ON "ProductPrices" ("ProductPriceTypeId1");

CREATE INDEX "IX_ProductPromotions_PromotionId" ON "ProductPromotions" ("PromotionId");

CREATE INDEX "IX_Products_BrandId" ON "Products" ("BrandId");

CREATE UNIQUE INDEX "IX_Products_SKU" ON "Products" ("SKU");

CREATE INDEX "IX_PromotionCategories_PromotionId" ON "PromotionCategories" ("PromotionId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260303203156_InitialPostgreSQL', '10.0.2');

