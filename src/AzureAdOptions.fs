module AzureAdOptions

let objectIdentifierType =
    "http://schemas.microsoft.com/identity/claims/objectidentifier"

let tenantIdType =
    "http://schemas.microsoft.com/identity/claims/tenantid"


type AzureAdOptions() =
    member val ClientId = "" with get, set
    member val ClientSecret = "" with get, set
    member val Instance = "" with get, set
    member val Domain = "" with get, set
    member val TenantId = "" with get, set
    member val CallbackPath = "" with get, set
    member val BaseUrl = "" with get, set
    member val Scopes = "" with get, set
    member val GraphResourceId = "" with get, set
    member val GraphScopes = "" with get, set

