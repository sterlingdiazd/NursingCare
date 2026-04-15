# Fixture Data Seeding Guide

## Overview

The system now automatically seeds comprehensive fixture data on application startup. This includes:

1. **Catalog Data**: All pricing categories, unit types, care request types, distance factors, complexity levels, nurse specialties, and compensation rules
2. **User Accounts**: 1 seeded admin, 22 nurse users, and 1 test client
3. **Care Requests**: 22 approved care requests with specific pricing for payroll testing

## Seeded Data

### Test Accounts

**Seeded Admin**
- Email: `sterlingdiazd@gmail.com`
- Password: `12345678`
- Role: Admin
- Created automatically on startup after migrations
- Reconciled on each startup to remain active and usable for login

**Test Client**
- Email: `client@test.com`
- Password: `12345678`
- Role: Client

**Nurse Accounts** (all with password `12345678`)
- Lorea (lorea@nurses.test)
- Charleny (charleny@nurses.test)
- Valentin (valentin@nurses.test)
- Marel (marel@nurses.test)
- Liliana (liliana@nurses.test)
- Clari (clari@nurses.test)
- Solano (solano@nurses.test)
- Angela Maria (angela.maria@nurses.test)
- Karen (karen@nurses.test)
- Cristina (cristina@nurses.test)
- Figueredo (figueredo@nurses.test)
- Annie (annie@nurses.test)
- Zoila (zoila@nurses.test)
- Maria Isabel (maria.isabel@nurses.test)
- Emilina (emilina@nurses.test)
- Cindy (cindy@nurses.test)
- Agustina (agustina@nurses.test)
- Johanna (johanna@nurses.test)
- Miranda (miranda@nurses.test)
- Miguelina (miguelina@nurses.test)
- Celai (celai@nurses.test)
- De Los Santos (de.los.santos@nurses.test)

### Care Requests & Revenue

Each nurse has one assigned care request with the following company revenues:

| Nurse | Amount |
|-------|--------|
| Lorea | $12,200 |
| Charleny | $12,200 |
| Valentin | $13,000 |
| Marel | $12,000 |
| Liliana | $15,000 |
| Clari | $15,000 |
| Solano | $15,000 |
| Angela Maria | $13,000 |
| Karen | $15,500 |
| Cristina | $15,500 |
| Figueredo | $23,000 |
| Annie | $23,000 |
| Zoila | $12,500 |
| Maria Isabel | $12,500 |
| Emilina | $15,000 |
| Cindy | $15,000 |
| Agustina | $15,000 |
| Johanna | $15,000 |
| Miranda | $13,200 |
| Miguelina | $13,000 |
| Celai | $12,500 |
| De Los Santos | $12,500 |

**Total Revenue**: $312,800

### Catalog Data

- **Care Request Categories**: Hogar, Domicilio, Medicos
- **Unit Types**: Dia completo, Mes, Medio dia, Sesion
- **Care Request Types**: 13 types across all categories
- **Distance Factors**: Local (1.0x), Cercana (1.1x), Media (1.2x), Lejana (1.3x)
- **Complexity Levels**: Estandar (1.0x), Moderada (1.1x), Alta (1.2x), Critica (1.3x)
- **Nurse Specialties**: 5 types (Adult Care, Pediatric Care, etc.)
- **Nurse Categories**: Junior, Semisenior, Senior, Lider
- **Compensation Rules**: 3 default rules for Hogar, Domicilio, and Medicos categories

## How Seeding Works

### Automatic Seeding

When the application starts:

1. Database migrations are applied (`ApplyMigrations()`)
2. The fixed development admin account is ensured and reconciled
3. Fixture data is seeded asynchronously (`SeedFixtureDataAsync()`)
4. Data is only seeded if it doesn't already exist (idempotent)

### Implementation Details

**Files Modified**:
- `Program.cs` - Added fixture seeding call
- `MigrationExtensions.cs` - Added `SeedFixtureDataAsync()` method
- `CatalogSeeding.cs` - Extended with admin, user, and nurse seeding
- `CareRequestSeeding.cs` - New file for care request seeding

### Key Features

✅ **Idempotent**: Seeding only occurs if data doesn't exist
✅ **Deterministic GUIDs**: Fixed GUIDs for reproducible test scenarios
✅ **Realistic Data**: Uses same pricing calculations as production
✅ **Async**: All database operations are async
✅ **Separated Concerns**: Catalogs and entities are seeded separately
✅ **Fixed Admin Access**: A deterministic admin account is always restored for local development

## Testing Use Cases

### 1. Care Request Creation with Nurse Assignment

Login as the test client (`client@test.com` / `12345678`) and:

1. Create a new care request
2. In the "Suggested Nurse" field, you'll see the autocomplete populated with all 22 nurses
3. Select any nurse (e.g., "Lorea")
4. The price will be calculated based on the care request type and factors

### 2. Payroll Calculations

With the care requests pre-configured:

1. Login as admin using `sterlingdiazd@gmail.com` / `12345678`
2. Navigate to Payroll
3. You'll see all 22 care requests are approved and assigned
4. Create a payroll period covering the care request dates
5. The system will calculate compensation based on:
   - Company revenue from each care request
   - Compensation rules for each category
   - Nurse category and specialty modifiers

### 3. Reporting

All reports can now be tested with real data:
- Care request analytics
- Nurse performance metrics
- Revenue tracking
- Payroll summaries

## Accessing Fixed GUIDs

If you need to reference specific nurses in code, use the `CatalogSeeding.NurseIds` dictionary:

```csharp
var loreaId = CatalogSeeding.NurseIds["Lorea"];
var testClientId = CatalogSeeding.TestClientId;
```

## Modifying Seeded Data

To change seeded data:

1. **Catalogs**: Edit `CatalogSeeding.cs` in the `SeedCatalogsAsync` method
2. **Users/Nurses**: Edit `CatalogSeeding.cs` in the `SeedUsersAndNursesAsync` method
3. **Care Requests**: Edit `CareRequestSeeding.cs` in the `EnsureSeededAsync` method

After changes, delete the database and restart the application to re-seed.

## Production Considerations

⚠️ **WARNING**: This seeding is designed for **development and testing only**.

For production:
- Create a separate admin account with strong password
- Remove or disable test accounts
- Use separate seeding for production fixtures
- Implement role-based access control

## Troubleshooting

### Data Not Seeding

1. Check database connection string
2. Verify migrations ran successfully
3. Check logs for errors in `SeedFixtureDataAsync`
4. Ensure database is writable

### Duplicate Records

If seeding fails midway:
1. Check the idempotency logic (uses `AnyAsync` checks)
2. Verify all foreign keys are correct
3. Run migrations fresh: `dotnet ef database drop --force && dotnet ef database update`

### Password Issues

All test accounts use password `12345678`. To change:

Edit `CatalogSeeding.cs`:
- Admin: `SeededAdminPassword`
- Line for client: `HashPassword("12345678")`
- Line for nurses: `HashPassword("12345678")`

## Next Steps

1. ✅ Run the application to seed data
2. ✅ Login with test accounts
3. ✅ Test care request creation and assignment
4. ✅ Calculate payroll from the fixture data
5. ✅ Run reports against the fixture data
