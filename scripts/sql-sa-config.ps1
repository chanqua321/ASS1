# Shared SQL auth settings for LocalDB + sa (dot-source from other scripts).
$script:SqlSaServer = "(localdb)\MSSQLLocalDB"
$script:SqlSaDatabase = "Assigment1DocDb"
$script:SqlSaUser = "sa"
$script:SqlSaPassword = "12345"
$script:SqlLocalDbInstance = "MSSQLLocalDB"

function Get-SqlSaConnectionString {
    "Server=$($script:SqlSaServer);Database=$($script:SqlSaDatabase);User Id=$($script:SqlSaUser);Password=$($script:SqlSaPassword);Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
}
