param([switch]$CheckOnly, [switch]$PreviewMissing, [switch]$SelfTest, [string]$PreviewPath)
$ErrorActionPreference='Stop'
function Find-Runtime {
    $paths=@("$env:ProgramFiles\dotnet\dotnet.exe","$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe","$env:USERPROFILE\.dotnet\dotnet.exe","$env:USERPROFILE\.codex\tools\dotnet-sdk-8\dotnet.exe")
    if($env:DOTNET_ROOT){$paths+="$env:DOTNET_ROOT\dotnet.exe"}
    $cmd=Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if($cmd){$paths+=$cmd.Source}
    foreach($p in ($paths|Select-Object -Unique)){
        if(Test-Path -LiteralPath $p){
            $versions=& $p --list-runtimes 2>$null
            if($LASTEXITCODE -eq 0 -and ($versions -match '^Microsoft.WindowsDesktop.App 8\.0\.\d+ ')){return $p}
        }
    }
    return $null
}
function Select-Installer($metadata,$arch){
    $release=$metadata.releases|Where-Object {$_.windowsdesktop.version -eq $metadata.'latest-runtime'}|Select-Object -First 1
    $file=$release.windowsdesktop.files|Where-Object {$_.rid -eq "win-$arch" -and $_.name -like '*.exe'}|Select-Object -First 1
    if(-not $file -or $file.hash -notmatch '^[a-fA-F0-9]{128}$'){throw 'SM105: Microsoft metadata did not contain a valid installer.'}
    $uri=[uri]$file.url
    if($uri.Scheme -ne 'https' -or $uri.Host -notin @('builds.dotnet.microsoft.com','download.visualstudio.microsoft.com','dotnetcli.azureedge.net')){throw 'SM106: Installer source rejected.'}
    return $file
}
function Test-Installer($path,$expected){
    if((Get-FileHash -LiteralPath $path -Algorithm SHA512).Hash -ne $expected){throw 'SM107: Download integrity check failed.'}
    $sig=Get-AuthenticodeSignature -LiteralPath $path
    if($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation(?:,|$)'){throw 'SM108: Microsoft publisher signature could not be verified.'}
}
$script:runtime=Find-Runtime
$script:dll=Join-Path $PSScriptRoot 'bin\Release\net8.0-windows\StickMouse.dll'
$script:files=(Test-Path $script:dll) -and (Test-Path ($script:dll -replace '\.dll$','.runtimeconfig.json')) -and (Test-Path ($script:dll -replace '\.dll$','.deps.json'))
$script:osReady=[Environment]::OSVersion.Version.Major -ge 10
$arch=$env:PROCESSOR_ARCHITEW6432
if(-not $arch){$arch=$env:PROCESSOR_ARCHITECTURE}
$script:arch=switch($arch){'AMD64'{'x64'} 'ARM64'{'arm64'} 'x86'{'x86'} default{throw 'SM104: Unsupported architecture.'}}
if($CheckOnly){[pscustomobject]@{Windows10OrLater=$script:osReady;Architecture=$script:arch;DesktopRuntimeAvailable=[bool]$script:runtime;ApplicationFilesAvailable=$script:files}|ConvertTo-Json;exit 0}
if($SelfTest){
    $meta=@{'latest-runtime'='8.0.99';releases=@(@{windowsdesktop=@{version='8.0.99';files=@(@{rid='win-x64';name='runtime.exe';url='https://builds.dotnet.microsoft.com/runtime.exe';hash=('a'*128)})}})}
    if((Select-Installer $meta 'x64').hash -ne ('a'*128)){throw 'Metadata test failed'}
    foreach($url in @('https://example.com/runtime.exe','http://builds.dotnet.microsoft.com/runtime.exe')){
        $meta.releases[0].windowsdesktop.files[0].url=$url;$rejected=$false
        try{Select-Installer $meta 'x64'|Out-Null}catch{$rejected=$true}
        if(-not $rejected){throw 'Untrusted source accepted'}
    }
    $fixture=Join-Path $PSScriptRoot '.verify/setup-integrity-test.txt'
    New-Item -ItemType Directory -Force (Split-Path $fixture)|Out-Null
    Set-Content -LiteralPath $fixture -Value 'not an installer'
    $rejected=$false
    try{Test-Installer $fixture ('0'*128)}catch{$rejected=$true}
    if(-not $rejected){throw 'Bad hash accepted'}
    $rejected=$false
    try{Test-Installer $fixture (Get-FileHash $fixture -Algorithm SHA512).Hash}catch{$rejected=$true}
    if(-not $rejected){throw 'Unsigned payload accepted'}
    'PASS: prerequisites, metadata, untrusted/HTTP source rejection, hash mismatch and unsigned payload rejection; no network or installation';exit 0
}
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
[xml]$xaml=@'
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Title="StickMouse - Ready check" Width="660" Height="620" MinWidth="580" MinHeight="500" Background="#14171A" Foreground="#EDF1EF" FontFamily="Segoe UI" FontSize="13" WindowStartupLocation="CenterScreen">
<Window.Resources><Style TargetType="Button"><Setter Property="Padding" Value="20,11"/><Setter Property="Background" Value="#9DDABE"/><Setter Property="Foreground" Value="#14291E"/><Setter Property="BorderThickness" Value="0"/><Setter Property="Cursor" Value="Hand"/></Style></Window.Resources>
<ScrollViewer VerticalScrollBarVisibility="Auto"><StackPanel Margin="32">
<TextBlock Text="STICKMOUSE / CODERKNIGHT" Foreground="#9DDABE" FontSize="12" FontWeight="SemiBold"/>
<TextBlock Text="A quick check before you play." FontSize="26" FontWeight="SemiBold" Margin="0,18,0,8"/>
<TextBlock Text="Select missing components to install, then choose Next." Foreground="#A3AEB3" TextWrapping="Wrap"/>
<Border Background="#1C2126" CornerRadius="12" Padding="20" Margin="0,24,0,20"><StackPanel>
<TextBlock Name="WindowsStatus" Margin="0,0,0,16" TextWrapping="Wrap"/>
<CheckBox Name="RuntimeCheck" Content=".NET 8 Desktop Runtime" Foreground="#EDF1EF" FontSize="15"/>
<TextBlock Name="RuntimeStatus" Foreground="#A3AEB3" TextWrapping="Wrap" Margin="21,8,0,16"/>
<TextBlock Name="InputStatus" Margin="0,0,0,16"/>
<TextBlock Name="FilesStatus" TextWrapping="Wrap"/>
</StackPanel></Border>
<TextBlock Text="CONTROLLER" Foreground="#9DDABE" FontSize="11" FontWeight="SemiBold"/>
<TextBlock Text="The app automatically detects XInput controllers. You can connect your gamepad after opening it." Margin="0,8,0,8" TextWrapping="Wrap"/>
<TextBlock Text="Native PlayStation controllers need a separately configured XInput adapter. No third-party drivers are installed here." Foreground="#A3AEB3" FontSize="12" TextWrapping="Wrap"/>
<TextBlock Text="Downloads come from Microsoft. Installation may ask for administrator approval. No forced restart." Foreground="#A3AEB3" FontSize="12" TextWrapping="Wrap" Margin="0,18,0,0"/>
<TextBlock Name="Progress" Text="No downloads until you choose Next." Foreground="#A3AEB3" TextWrapping="Wrap" Margin="0,18,0,18"/>
<Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
<Button Name="Refresh" Content="Check again" HorizontalAlignment="Left" Background="#252D32" Foreground="#EDF1EF"/>
<Button Name="Cancel" Content="Cancel" Grid.Column="1" Background="#252D32" Foreground="#EDF1EF" Margin="0,0,10,0"/>
<Button Name="Next" Content="Next" Grid.Column="2" MinWidth="95"/></Grid>
</StackPanel></ScrollViewer></Window>
'@
$script:window=[Windows.Markup.XamlReader]::Load((New-Object System.Xml.XmlNodeReader $xaml))
foreach($name in @('WindowsStatus','RuntimeCheck','RuntimeStatus','InputStatus','FilesStatus','Progress','Refresh','Cancel','Next')){Set-Variable -Name $name -Scope Script -Value $script:window.FindName($name)}
$script:busy=$false
function Refresh-Checks{
    $script:runtime=Find-Runtime
    if($PreviewMissing){$script:runtime=$null}
    $script:WindowsStatus.Text=if($script:osReady){'Available   Windows 10 / 11'}else{'Required   Upgrade to Windows 10 or later'}
    $script:RuntimeCheck.IsChecked=-not [bool]$script:runtime
    $script:RuntimeCheck.IsEnabled=-not [bool]$script:runtime
    $script:RuntimeStatus.Text=if($script:runtime){'Available. No installation needed.'}else{"Missing. Required to open StickMouse ($script:arch). Select to install."}
    $script:InputStatus.Text='Available   Windows XInput (built into Windows)'
    $script:FilesStatus.Text=if($script:files){'Available   StickMouse application files'}else{'Missing   Extract the complete StickMouse application folder'}
}
function Pump{$script:window.Dispatcher.Invoke([action]{},[Windows.Threading.DispatcherPriority]::Background)}
function Wait-Download($task,$client){
    $watch=[Diagnostics.Stopwatch]::StartNew()
    while(-not $task.IsCompleted){Pump;if($watch.Elapsed.TotalSeconds -gt 180){$client.CancelAsync();throw 'SM112: Download timed out. Check your connection and retry.'};Start-Sleep -Milliseconds 50}
    $task.GetAwaiter().GetResult()
}
$script:Refresh.Add_Click({Refresh-Checks})
$script:Cancel.Add_Click({$script:window.Close()})
$script:window.Add_Closing({param($sender,$e)if($script:busy){$e.Cancel=$true;$script:Progress.Text='Please wait for the current download or installation to finish.'}})
$script:Next.Add_Click({
    if($PreviewMissing -or $PreviewPath){$script:Progress.Text='Preview only. Installation and launch disabled.';return}
    if(-not $script:osReady -or -not $script:files){$script:Progress.Text='Resolve the Windows or application-file requirement above first.';return}
    if(-not $script:runtime -and -not $script:RuntimeCheck.IsChecked){$script:Progress.Text='The Desktop Runtime is required. Select it to install, or Cancel.';return}
    $script:busy=$true;$script:Next.IsEnabled=$false;$script:Refresh.IsEnabled=$false;$script:Cancel.IsEnabled=$false
    $downloadDir=$null
    try{
        if(-not $script:runtime){
            $script:Progress.Text='Reading Microsoft release information...';Pump
            [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
            $client=New-Object Net.WebClient
            try{
                $json=Wait-Download ($client.DownloadStringTaskAsync('https://builds.dotnet.microsoft.com/dotnet/release-metadata/8.0/releases.json')) $client
                $file=Select-Installer ($json|ConvertFrom-Json) $script:arch
                $downloadDir=Join-Path ([IO.Path]::GetTempPath()) ('StickMouse-'+[guid]::NewGuid().ToString('N'))
                New-Item -ItemType Directory -Path $downloadDir|Out-Null
                $installer=Join-Path $downloadDir 'desktop-runtime.exe'
                $script:Progress.Text='Downloading .NET Desktop Runtime from Microsoft...';Pump
                Wait-Download ($client.DownloadFileTaskAsync([uri]$file.url,$installer)) $client
            }finally{$client.Dispose()}
            $script:Progress.Text='Verifying SHA-512 and Microsoft publisher signature...';Pump
            Test-Installer $installer $file.hash
            $script:Progress.Text='Installing... Approve the Windows prompt if asked.';Pump
            $process=Start-Process -FilePath $installer -ArgumentList '/install /passive /norestart' -Verb RunAs -PassThru
            while(-not $process.HasExited){Pump;Start-Sleep -Milliseconds 100;$process.Refresh()}
            if($process.ExitCode -notin @(0,3010,1641)){throw "SM110: Installer exit code $($process.ExitCode). Installation did not complete."}
            $script:runtime=Find-Runtime
            if(-not $script:runtime){throw 'SM111: Runtime not detected after installation. Restart Windows if requested, then retry.'}
        }
        $script:Progress.Text='Ready. Opening StickMouse...';Pump
        Start-Process -FilePath $script:runtime -ArgumentList ('"'+$script:dll+'"') -WorkingDirectory $PSScriptRoot -WindowStyle Hidden|Out-Null
        $script:busy=$false;$script:window.Close()
    }catch{
        $script:Progress.Text=$_.Exception.Message
        try{$dir=Join-Path $env:LOCALAPPDATA 'StickMouse';New-Item -ItemType Directory -Force $dir|Out-Null;Add-Content (Join-Path $dir 'setup.log') ((Get-Date).ToUniversalTime().ToString('o')+' '+$_.Exception.Message)}catch{}
    }finally{
        $script:busy=$false;$script:Next.IsEnabled=$true;$script:Refresh.IsEnabled=$true;$script:Cancel.IsEnabled=$true
        # Keep the verified installer in its unique temp folder for Windows maintenance/diagnosis.
    }
})
Refresh-Checks
if($PreviewPath){
    $script:window.Add_ContentRendered({
        try {
            $dpi=144
            $bitmap=New-Object Windows.Media.Imaging.RenderTargetBitmap ([int]($script:window.ActualWidth*1.5)),([int]($script:window.ActualHeight*1.5)),$dpi,$dpi,([Windows.Media.PixelFormats]::Pbgra32)
            $bitmap.Render($script:window)
            $encoder=New-Object Windows.Media.Imaging.PngBitmapEncoder
            $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
            $stream=[IO.File]::Create($PreviewPath)
            try{$encoder.Save($stream)}finally{$stream.Dispose()}
        }finally{$script:window.Close()}
    })
}
$script:window.ShowDialog()|Out-Null
