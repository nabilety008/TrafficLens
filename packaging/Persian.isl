; *** Inno Setup Persian/Farsi language file for TrafficLens ***
;
;   Created for TL-024 — Installer Localization.
;   Language: Persian (Farsi) — LanguageID=$0429, CodePage=1256
;   RTL layout enabled.
;

[LangOptions]
LanguageName=فارسی
LanguageID=$0429
LanguageCodePage=1256
RightToLeft=yes

[Messages]

; *** Application titles
SetupAppTitle=نصب
SetupWindowTitle=نصب - %1
UninstallAppTitle=حذف
UninstallAppFullTitle=حذف %1

; *** Misc. common
InformationTitle=اطلاعات
ConfirmTitle=تأیید
ErrorTitle=خطا

; *** SetupLdr messages
SetupLdrStartupMessage=این برنامه %1 را روی رایانه شما نصب خواهد کرد. آیا می‌خواهید ادامه دهید؟
LdrCannotCreateTemp=خطا در ایجاد پرونده موقت. نصب بسته خواهد شد
LdrCannotExecTemp=امکان اجرای پرونده در پوشه موقت وجود نارد. ادامه نصب ممکن نیست

; *** Startup error messages
LastErrorMessage=%1.%n%nخطا %2: %3
SetupFileMissing=پرونده %1 در پوشه نصب یافت نشد. لطفاً مشکل را برطرف کنید یا با یک نسخه جدید از برنامه امتحان کنید.
SetupFileCorrupt=پرونده‌های نصب خراب هستند. لطفاً با یک نسخه جدید از برنامه نصب کنید.
SetupFileCorruptOrWrongVer=پرونده‌های نصب خراب یا ناسازگار با این نسخه از برنامه نصب هستند. لطفاً مشکل را برطرف کنید یا برنامه را از یک نسخه جدید نصب کنید.
InvalidParameter=پارامتر نامعتبر در خط فرمان وارد شده:%n%n%1
SetupAlreadyRunning=یک نصب دیگر در حال اجراست.
WindowsVersionNotSupported=این برنامه در سیستم‌عامل شما پشتیبانی نمی‌شود.
WindowsServicePackRequired=برنامه نیاز به نصب %1 به‌روزرسانی %2 یا بالاتر دارد.
NotOnThisPlatform=این برنامه روی %1 اجرا نخواهد شد.
OnlyOnThisPlatform=این برنامه باید روی %1 اجرا شود.
OnlyOnTheseArchitectures=این برنامه فقط روی نسخه‌های ویندوز با معماری پردازنده‌های زیر قابل نصب است:%n%n%1
WinVersionTooLowError=این برنامه حداقل به %1 نسخه %2 نیاز دارد.
WinVersionTooHighError=این برنامه روی %1 نسخه %2 یا بالاتر قابل نصب نیست.
AdminPrivilegesRequired=برای نصب این برنامه باید با حساب مدیر وارد شوید.
PowerUserPrivilegesRequired=برای نصب این برنامه باید با حساب مدیر یا عضو گروه «کاربران پیشرفته» وارد شوید.
SetupAppRunningError=برنامه نصب تشخیص داده است که %1 در حال حاضر در حال اجراست.%n%nلطفاً تمام پنجره‌های آن را ببندید و برای ادامه «تأیید» یا برای خروج «لغو» را فشار دهید.
UninstallAppRunningError=برنامه حذف تشخیص داده است که %1 در حال حاضر در حال اجراست.%n%nلطفاً تمام پنجره‌های آن را ببندید و برای ادامه «تأیید» یا برای خروج «لغو» را فشار دهید.

; *** Startup questions
PrivilegesRequiredOverrideTitle=حالت نصب را انتخاب کنید
PrivilegesRequiredOverrideInstruction=حالت نصب را انتخاب کنید
PrivilegesRequiredOverrideText1=%1 می‌تواند برای همه کاربران (نیاز به مدیریت) یا فقط برای شما نصب شود.
PrivilegesRequiredOverrideText2=%1 می‌تواند فقط برای شما یا برای همه کاربران (نیاز به مدیریت) نصب شود.
PrivilegesRequiredOverrideAllUsers=برای &همه کاربران نصب شود
PrivilegesRequiredOverrideAllUsersRecommended=برای &همه کاربران نصب شود (توصیه‌شده)
PrivilegesRequiredOverrideCurrentUser=فقط برای &من نصب شود
PrivilegesRequiredOverrideCurrentUserRecommended=فقط برای &من نصب شود (توصیه‌شده)

; *** Misc. errors
ErrorCreatingDir=برنامه نصب نتوانست پوشه "%1" را ایجاد کند
ErrorTooManyFilesInDir=امکان ایجاد پرونده در پوشه "%1" به دلیل تعداد زیاد پرونده‌ها وجود ندارد

; *** Setup common messages
ExitSetupTitle=خروج از نصب
ExitSetupMessage=هنوز نصب کامل نشده است. اگا اکنون خارج شوید، برنامه نصب نخواهد شد.%n%nمی‌توانید بعداً برنامه نصب را دوباره اجرا کنید تا نصب کامل شود.%n%nآیا مطمئن هستید که می‌خواهید خارج شوید؟
AboutSetupMenuItem=&درباره برنامه نصب...
AboutSetupTitle=درباره برنامه نصب
AboutSetupMessage=%1 نسخه %2%n%3%n%n صفحه اصلی %1:%n%4
AboutSetupNote=

; *** Buttons
ButtonBack=< &قبلی
ButtonNext=&بعدی >
ButtonInstall=&نصب
ButtonOK=تأیید
ButtonCancel=لغو
ButtonYes=&بله
ButtonYesToAll=بله برای &همه
ButtonNo=&خیر
ButtonNoToAll=خیر برای &همه
ButtonFinish=&پایان
ButtonBrowse=&مرور...
ButtonWizardBrowse=مرور...
ButtonNewFolder=&ایجاد پوشه جدید

; *** "Select Language" dialog messages
SelectLanguageTitle=زبان نصب را انتخاب کنید
SelectLanguageLabel=زبان برنامه نصب را انتخاب کنید.

; *** Common wizard text
ClickNext=برای ادامه «بعدی» یا برای خروج «لغو» را فشار دهید.
BeveledLabel=
BrowseDialogTitle=انتخاب پوشه
BrowseDialogLabel=از فهرست یک پوشه انتخاب کنید و «تأیید» را فشار دهید.
NewFolderName=پوشه جدید

; *** "Welcome" wizard page
WelcomeLabel1=به برنامه نصب [name] خوش آمدید
WelcomeLabel2=این جادوگر شما را در فرآیند نصب [name/ver] روی رایانه‌تان راهنمایی خواهد کرد.%n%nتوصیه می‌شود قبل از نصب تمام برنامه‌های فعال را ببندید.

; *** "Password" wizard page
WizardPassword=رمز عبور
PasswordLabel1=نصب با رمز عبور محافظت می‌شود.
PasswordLabel3=لطفاً رمز عبور را وارد کرده و «بعدی» را فشار دهید. در حروف لاتین، بین حروف کوچک و بزرگ تفاوت وجود دارد.
PasswordEditLabel=&رمز عبور:
IncorrectPassword=رمز عبور واردشده نادرست است. لطفاً دوباره امتحان کنید.

; *** "License Agreement" wizard page
WizardLicense=مجوز استفاده
LicenseLabel=لطفاً قبل از ادامه نصب اطلاعات مهم زیر را بخوانید.
LicenseLabel3=لطفاً مجوز استفاده زیر را بخوانید. برای ادامه نصب باید شرایط این توافقنامه را بپذیرید.
LicenseAccepted=من توافقنامه را می‌پذیرم
LicenseNotAccepted=من توافقنامه را نمی‌پذیرم

; *** "Information" wizard pages
WizardInfoBefore=اطلاعات
InfoBeforeLabel=لطفاً قبل از ادامه نصب اطلاعات مهم زیر را بخوانید.
InfoBeforeClickLabel=هنگامی که آماده ادامه نصب هستید، «بعدی» را فشار دهید.
WizardInfoAfter=اطلاعات
InfoAfterLabel=لطفاً قبل از ادامه نصب اطلاعات مهم زیر را بخوانید.
InfoAfterClickLabel=هنگامی که آماده ادامه نصب هستید، «بعدی» را فشار دهید.

; *** "Select Destination Location" wizard page
WizardSelectDir=انتخاب مقصد نصب
SelectDirDesc=[name] کجا نصب شود؟
SelectDirLabel3=برنامه نصب [name] را در پوشه زیر نصب خواهد کرد.
SelectDirBrowseLabel=برای ادامه «بعدی» را فشار دهید. اگر می‌خواهید پوشه دیگری را انتخاب کنید، «مرور» را فشار دهید.
DiskSpaceGBLabel=حداقل [gb] گیگابایت فضای خالی دیسک برای نصب نیاز است.
DiskSpaceMBLabel=حداقل [mb] مگابایت فضای خالی دیسک برای نصب نیاز است.
CannotInstallToNetworkDrive=امکان نصب روی درایو شبکه وجود ندارد.
CannotInstallToUNCPath=امکان نصب در مسیر UNC وجود نارد.
InvalidPath=باید مسیر کامل با حرف درایو وارد شود؛ به عنوان مثال:%n%nC:\APP%n%nیا مسیر UNC به صورت:%n%n\\server\share
InvalidDrive=درایو یا اشتراک UNC انتخابی وجود ندارد یا در دسترس نیست. لطفاً درایو یا اشتراک دیگری را انتخاب کنید.
DiskSpaceWarningTitle=فضای خالی کافی نیست
DiskSpaceWarning=حداقل %1KB فضای خالی دیسک برای نصب نیاز است، اما درایو انتخابی فقط %2KB فضای خالی دارد. آیا می‌خواهید با این وجود ادامه دهید؟
DirNameTooLong=نام پوشه یا مسیر آن بیش از حد طولانی است.
InvalidDirName=نام پوشه نامعتبر است.
BadDirName32=نام پوشه نمی‌تواند شامل کاراکترهای زیر باشد:%n%n%1
DirExistsTitle=پوشه وجود دارد
DirExists=پوشه:%n%n%1%n%nاز قبل وجود دارد. آیا می‌خواهید در این پوشه نصب کنید؟
DirDoesntExistTitle=پوشه وجود ندارد
DirDoesntExist=پوشه:%n%n%1%n%nوجود ندارد. آیا می‌خواهید برنامه نصب آن را ایجاد کند؟

; *** "Select Components" wizard page
WizardSelectComponents=انتخاب مولفه‌ها
SelectComponentsDesc=کدام مولفه‌ها نصب شوند؟
SelectComponentsLabel2=مولفه‌های مورد نظر را انتخاب کنید. برای ادامه «بعدی» را فشار دهید.
FullInstallation=نصب کامل
CompactInstallation=نصب پایه
CustomInstallation=نصب سفارشی

; *** "Select Additional Tasks" wizard page
WizardSelectTasks=انتخاب وظایف اضافی
SelectTasksDesc=چه وظایف اضافی توسط برنامه نصب انجام شود؟
SelectTasksLabel2=وظایف اضافی مورد نظر را هنگام نصب [name] انتخاب کنید و سپس «بعدی» را فشار دهید.

; *** "Select Start Menu Folder" wizard page
WizardSelectProgramGroup=انتخاب پوشه منوی شروع
SelectStartMenuFolderDesc=کجا میانبرهای برنامه قرار گیرد؟
SelectStartMenuFolderLabel3=برنامه نصب میانبرهای برنامه را در پوشه زیر از منوی شروع ایجاد خواهد کرد.
SelectStartMenuFolderBrowseLabel=برای ادامه «بعدی» را فشار دهید. اگر می‌خواهید پوشه دیگری را انتخاب کنید، «مرور» را فشار دهید.
MustEnterGroupName=باید نام پوشه را وارد کنید.
GroupNameTooLong=نام پوشه یا مسیر آن بیش از حد طولانی است.
InvalidGroupName=نام پوشه نامعتبر است.
BadGroupName=نام پوشه نمی‌تواند شامل کاراکترهای زیر باشد:%n%n%1
NoProgramGroupCheck2=&پوشه‌ای در منوی شروع ایجاد نشود

; *** "Ready to Install" wizard page
WizardReady=آماده نصب
ReadyLabel1=برنامه نصب اکنون آماده نصب [name] روی رایانه شماست.
ReadyLabel2a=برای نصب «نصب» را فشار دهید، یا اگر می‌خواهید تنظیمات را تغییر دهید «بازگشت» را فشار دهید.
ReadyLabel2b=برای ادامه نصب «نصب» را فشار دهید.
ReadyMemoUserInfo=اطلاعات کاربر:
ReadyMemoDir=مقصد نصب:
ReadyMemoType=نوع نصب:
ReadyMemoComponents=مولفه‌های انتخابی:
ReadyMemoGroup=پوشه منوی شروع:
ReadyMemoTasks=وظایف اضافی:

; *** "Preparing to Install" wizard page
WizardPreparing=آماده‌سازی برای نصب
PreparingDesc=برنامه نصب در حال آماده‌سازی برای نصب [name] روی رایانه شماست.
PreviousInstallNotCompleted=نصب/حذف برنامه قبلی کامل نشده است. برای تکمیل آن باید رایانه را مجدداً راه‌اندازی کنید.%n%nپس از راه‌اندازی مجدد، برنامه نصب را دوباره اجرا کنید تا [name] نصب شود.
CannotContinue=برنامه نصب نمی‌تواند ادامه دهد. لطفاً «لغو» را فشار دهید.
ApplicationsFound=برنامه‌های زیر از پرونده‌هایی استفاده می‌کنند که باید توسط برنامه نصب به‌روز شوند. توصیه می‌شود اجازه دهید برنامه نصب این برنامه‌ها را به صورت خودکار ببندد.
ApplicationsFound2=برنامه‌های زیر از پرونده‌هایی استفاده می‌کنند که باید توسط برنامه نصب به‌روز شوند. توصیه می‌شود اجازه دهید برنامه نصب این برنامه‌ها را به صورت خودکار ببندد. پس از نصب، برنامه نصب سعی خواهد کرد همان برنامه‌ها را دوباره باز کند.
CloseApplications=برنامه‌ها به صورت خودکار &بسته شوند
DontCloseApplications=این برنامه‌ها &بسته نشوند
ErrorCloseApplications=برنامه نصب نمی‌تواند فرآیندها را به صورت خودکار ببندد. توصیه می‌شود تمام برنامه‌هایی که از پرونده‌های مورد نیاز برای به‌روزرسانی استفاده می‌کنند را ببندید و سپس نصب را ادامه دهید.

; *** "Installing" wizard page
WizardInstalling=در حال نصب
InstallingLabel=لطفاً منتظر بمانید تا برنامه نصب [name] را روی رایانه شما نصب کند.

; *** "Setup Completed" wizard page
FinishedHeadingLabel=تکمیل نصب [name]
FinishedLabelNoIcons=نصب [name] روی رایانه شما با موفقیت انجام شد.
FinishedLabel=نصب [name] روی رایانه شما با موفقیت انجام شد. برای اجرای برنامه میانبرهای ایجادشده را فشار دهید.
ClickFinish=برای خروج «پایان» را فشار دهید.

; *** "Setup Needs the Next Disk" stuff
ChangeDiskTitle=دیسک بعدی برای ادامه نصب مورد نیاز است
SelectDiskLabel2=لطفاً دیسک شماره %1 را وارد کرده و «تأیید» را فشار دهید.%n%nاگر پرونده‌ها در پوشه دیگری هستند، مسیر صحیح را وارد کنید یا «مرور» را فشار دهید.
PathLabel=&مسیر:

; *** Installation phase messages
SetupAborted=فرآیند نصب کامل نشد.%n%nلطفاً مشکل را برطرف کرده و دوباره امتحان کنید.

; *** Installation status messages
StatusClosingApplications=در حال بستن برنامه‌ها...
StatusCreateDirs=در حال ایجاد پوشه‌ها...
StatusExtractFiles=در حال استخراج پرونده‌ها...
StatusCreateIcons=در حال ایجاد میانبرها...
StatusCreateRegistryEntries=در حال ایجاد رکوردهای رجیستری...
StatusSavingUninstall=در حال ذخیره اطلاعات حذف...
StatusRunProgram=تکمیل نصب...
StatusRollback=در حال بازگشت تغییرات...

; *** Misc. errors
ErrorInternal2=خطای داخلی: %1
ErrorFunctionFailedNoCode=%1 ناموفق بود
ErrorFunctionFailed=%1 ناموفق بود؛ کد %2
ErrorFunctionFailedWithMessage=%1 ناموفق بود؛ کد %2.%n%3
ErrorExecutingProgram=خطا در اجرای پرونده:%n%1

; *** Uninstall display name markings
UninstallDisplayNameMark=%1 (%2)
UninstallDisplayNameMarks=%1 (%2, %3)

; *** Uninstaller messages
UninstallNotFound=پرونده "%1" وجود نارد. ادامه ممکن نیست.
UninstallOpenError=امکان باز کردن پرونده "%1" وجود نارد. ادامه ممکن نیست.
UninstallUnsupportedVer=پرونده حذف "%1" با این نسخه از برنامه حذف سازگار نیست. ادامه ممکن نیست.
UninstallUnknownEntry=رکورد ناشناخته (%1) در پرونده حذف شناسایی شد.
ConfirmUninstall=آیا مطمئن هستید که می‌خواهید %1 و تمام مولفه‌های آن را کاملاً حذف کنید؟
UninstallStatusLabel=لطفاً منتظر بمانید تا %1 از رایانه شما حذف شود.
UninstalledAll=%1 با موفقیت از رایانه شما حذف شد.
UninstalledMost=حذف %1 کامل شد.%n%nبرخی مولفه‌ها حذف نشدند اما می‌توانید آنها را به صورت دستی حذف کنید.
UninstallDataCorrupted=پرونده "%1" خراب است. ادامه ممکن نیست.

; *** Uninstallation phase messages
WizardUninstalling=در حال حذف
StatusUninstalling=در حال حذف %1...

; *** Shutdown block reasons
ShutdownBlockReasonInstallingApp=در حال نصب %1.
ShutdownBlockReasonUninstallingApp=در حال حذف %1.

; *** Custom messages
[CustomMessages]

NameAndVersion=%1 نسخه %2
AdditionalIcons=میانبرهای اضافی:
CreateDesktopIcon=ایجاد میانبر روی &رومیزی
CreateQuickLaunchIcon=ایجاد میانبر در نوار &سریع
ProgramOnTheWeb=%1 در وب
UninstallProgram=حذف %1
LaunchProgram=اجرا %1
