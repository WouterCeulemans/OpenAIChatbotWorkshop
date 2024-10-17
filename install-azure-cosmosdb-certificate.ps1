$parameters = @{
    Uri = 'https://localhost:8081/_explorer/emulator.pem'
    Method = 'GET'
    OutFile = 'emulatorcert.crt'
    SkipCertificateCheck = $True
}
Invoke-WebRequest @parameters

$parameters = @{
    FilePath = 'emulatorcert.crt'
    CertStoreLocation = 'Cert:\CurrentUser\Root'
}
Import-Certificate @parameters