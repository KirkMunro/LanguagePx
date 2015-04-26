## LanguagePx

### Overview

TODO

### Minimum requirements

- PowerShell 4.0
- SnippetPx module

### License and Copyright

Copyright 2015 Kirk Munro

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

### Installing the LanguagePx module

LanguagePx is dependent on the SnippetPx module. You can download and install the
latest versions of LanguagePx and SnippetPx using any of the following methods:

#### PowerShellGet

If you don't know what PowerShellGet is, it's the way of the future for PowerShell
package management. If you're curious to find out more, you should read this:
<a href="http://blogs.msdn.com/b/mvpawardprogram/archive/2014/10/06/package-management-for-powershell-modules-with-powershellget.aspx" target="_blank">Package Management for PowerShell Modules with PowerShellGet</a>

Note that these commands require that you have the PowerShellGet module installed
on the system where they are invoked.

NOTE: COMING SOON (PowerShellGet support will be added once LanguagePx has a stable
release and is no longer purely experimental)

```powershell
# If you don’t have LanguagePx installed already and you want to install it for all
# all users (recommended, requires elevation)
Install-Module LanguagePx,SnippetPx

# If you don't have LanguagePx installed already and you want to install it for the
# current user only
Install-Module LanguagePx,SnippetPx -Scope CurrentUser

# If you have LanguagePx installed and you want to update it
Update-Module
```

#### PowerShell 4.0 or Later

To install from PowerShell 4.0 or later, open a native PowerShell console (not ISE,
unless you want it to take longer), and invoke one of the following commands:

```powershell
# If you want to install LanguagePx for all users or update a version already installed
# (recommended, requires elevation for new install for all users)
& ([scriptblock]::Create((iwr -uri http://tinyurl.com/Install-GitHubHostedModule).Content)) -ModuleName SnippetPx
& ([scriptblock]::Create((iwr -uri http://tinyurl.com/Install-GitHubHostedModule).Content)) -ModuleName LanguagePx -Branch master

# If you want to install LanguagePx for the current user
& ([scriptblock]::Create((iwr -uri http://tinyurl.com/Install-GitHubHostedModule).Content)) -ModuleName SnippetPx -Scope CurrentUser
& ([scriptblock]::Create((iwr -uri http://tinyurl.com/Install-GitHubHostedModule).Content)) -ModuleName LanguagePx -Scope CurrentUser -Branch master
```

### Using the LanguagePx module

TODO

That should give you a good idea of what is included in this module. If you have
ideas on what else you might like to see related to the creation of domain-specific
languages, please let me know on the GitHub page.