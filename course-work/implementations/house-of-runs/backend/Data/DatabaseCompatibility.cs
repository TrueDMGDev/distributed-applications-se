using Microsoft.EntityFrameworkCore;

namespace HouseOfRuns.Api.Data;

public static class DatabaseCompatibility
{
    public static async Task EnsureAsync(HouseOfRunsDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "Users"
            ADD COLUMN IF NOT EXISTS "Role" character varying(20) NOT NULL DEFAULT 'User';

            CREATE TABLE IF NOT EXISTS "RunComments" (
                "Id" uuid NOT NULL,
                "RunId" uuid NOT NULL,
                "UserId" uuid NOT NULL,
                "Body" character varying(500) NOT NULL,
                "IsEdited" boolean NOT NULL DEFAULT FALSE,
                "IsDeleted" boolean NOT NULL DEFAULT FALSE,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_RunComments" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_RunComments_Runs_RunId" FOREIGN KEY ("RunId") REFERENCES "Runs" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_RunComments_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS "IX_RunComments_RunId_CreatedAt"
                ON "RunComments" ("RunId", "CreatedAt");

            CREATE INDEX IF NOT EXISTS "IX_RunComments_UserId_CreatedAt"
                ON "RunComments" ("UserId", "CreatedAt");

            CREATE TABLE IF NOT EXISTS "RunLikes" (
                "Id" uuid NOT NULL,
                "RunId" uuid NOT NULL,
                "UserId" uuid NOT NULL,
                "Value" integer NOT NULL DEFAULT 1,
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_RunLikes" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_RunLikes_Runs_RunId" FOREIGN KEY ("RunId") REFERENCES "Runs" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_RunLikes_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RunLikes_RunId_UserId"
                ON "RunLikes" ("RunId", "UserId");

            CREATE INDEX IF NOT EXISTS "IX_RunLikes_UserId_CreatedAt"
                ON "RunLikes" ("UserId", "CreatedAt");

            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'Boons' AND column_name = 'Rarity'
                ) THEN
                    ALTER TABLE "Boons" ALTER COLUMN "Rarity" SET DEFAULT '';
                    ALTER TABLE "Boons" ALTER COLUMN "Rarity" DROP NOT NULL;
                END IF;

                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'RunBoons' AND column_name = 'RarityUsed'
                ) THEN
                    ALTER TABLE "RunBoons" ALTER COLUMN "RarityUsed" SET DEFAULT '';
                    ALTER TABLE "RunBoons" ALTER COLUMN "RarityUsed" DROP NOT NULL;
                END IF;
            END $$;

            ALTER TABLE "RunBoons"
            DROP COLUMN IF EXISTS "PickedInBiome";
            """);
    }
}
