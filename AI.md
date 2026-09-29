שאלתי:
אני רוצה ווידג'ט לווינדוס 11, כפי שעשיתי ב:
\
[https://github.com/Y-PLONI/NetFreeWidget](https://github.com/Y-PLONI/NetFreeWidget)
הווידג'ט יציג את החדשות מ'הגזרה' - [https://hagizra.news/](https://hagizra.news/)
הוא צריך להיות קל, מהיר, צורך כמה שפחות RAM וCPU.
הוא צריך לאפשר התראות - כבוי כברירת מחדל, להציג את החדשות בצורה נוחה, שגם בווידג'ט קטן יחסית (מצורף גם צילום מסך של גודל ווידגט' בינוני אצלי) - יהיה ניתן לקרוא בנוחות.
צרפתי צילומי מסך של 'הגזרה'.
אתה לא כותב קוד: אתה חוקר את מבנה האתר לעומק, איך למשוך את החדשות, ועוד...
היה עמוק וחכם!!

והAI ענה:
כן. בדקתי את זה מכמה כיוונים: את מבנה ה־NetFreeWidget שלך עצמו, את ההתנהגות של אתר הגיזרה, קוד ציבורי של לקוח אחר שכבר מושך ממנו חדשות, את תוסף Chrome הרשמי/קשור לאתר, ואת מגבלות ה־Widgets העדכניות של Windows 11.

המסקנה המרכזית: **לא כדאי לטעון את האתר, לא כדאי WebView2, ולא כדאי לגרד HTML. יש ל"הגיזרה" API JSON פשוט למדי שאפשר לדבר איתו ישירות.** זה מאפשר לבנות Widget קטן מאוד מבחינת עבודה שוטפת, ובמידה שההתראות כבויות — כמעט בלי פעילות רקע בכלל.

יש הסתייגות אחת: סביבת המחקר שלי לא הצליחה כרגע לבצע DNS ישירות ל־`hagizra.news`, ולכן לא אימתתי היום מול השרת עצמו headers כגון ETag/Cache-Control או את דוגמת ה־JSON החיה. את מבנה ה־API אימתתי דרך קוד ציבורי פעיל שצורך את הגיזרה, דרך קישורי המדיה הציבוריים של הגיזרה, ודרך מבנה ה־frontend. לכן יש כאן בסיס חזק מאוד, אבל הייתי מוסיף בתחילת הפיתוח בדיקת packet/JSON קצרה מהמחשב שלך לפני שמקבעים DTO סופי.

## הדבר החשוב ביותר שמצאתי: יש API

מצאתי בפרויקט הציבורי **Channel Center** לקוח שכבר יודע להציג את הגיזרה. הוא אינו מנסה לקרוא את ה־DOM של האתר אלא קורא את ה־API שלו ישירות. [viewer.js של Channel Center](https://github.com/Lev-Good/Channel-Center-Website/blob/main/viewer.js)

החוזה שהוא משתמש בו נראה כך:

| פעולה | API שנמצא | הערות |
|---|---|---|
| החדשות האחרונות | `/api/messages?limit=15&offset=0` | בעמוד אחר הוא משתמש ב־25 |
| עוד חדשות | `/api/messages?limit=25&offset=<offset>` | pagination פשוט |
| סינון | `&category=<category>` | נוסף לאותה בקשת messages |
| קטגוריות | `/api/categories` | מאפשר בעתיד בחירת קטגוריה בווידג'ט |
| בדיקת חדשות חדשות | `/api/messages?limit=10&offset=0` | הלקוח הציבורי בודק כך כל 15 שניות |
| מדיה | `/api/files/<hash>` | נמצאו קישורים ציבוריים אמיתיים בפורומים ברשת :chatgpt-content-reference{index="1"} |

יותר מזה: הקוד כבר מגלה לנו את ה־schema המשמעותי. הוא קורא לפחות `id`, `text`, `timestamp` או `time`, `author`, `views` ו־`reactions`. הוא גם בנוי לקבל שתי צורות תשובה: או מערך JSON ישירות, או אובייקט שמכיל `messages`.

זה מאוד שימושי לעמידות: הייתי עושה גם אצלך parser סלחני בדיוק באותה רוח, ולא מחבר את כל התוכנה למבנה JSON נוקשה מדי.

### המדיה קצת מעניינת

תמונה/וידאו/אודיו אינם בהכרח שדות JSON נפרדים. בלקוח שמצאתי הם מחולצים מתוך `text`.

הוא מזהה בין השאר:

`![...](...)`

וגם markers בסגנון:

`[image-embedded#](...)`

`[video-embedded#](...)`

`[audio-embedded#](...)`

אם הכתובת יחסית, הלקוח מחבר אותה ל־base URL של הערוץ. זה גם משתלב עם העובדה שנמצאו בפועל קישורים ציבוריים בנוסח `/api/files/<hash>`. :chatgpt-content-reference{index="2"}

כלומר הייתי מגדיר אצלך שכבה אחת בלבד שיודעת מהו "הגיזרה": היא מקבלת message גולמי ומוציאה `NewsItem` נקי: מזהה, זמן, טקסט נקי, מדיה, תגובות וכו'. **ה־Widget עצמו לא צריך לדעת שום דבר על syntax כמו `[video-embedded#]`.**

---

# למה בשום אופן לא הייתי מגרד את האתר

ה־frontend עצמו דינמי. סקריפט ציבורי שנכתב במיוחד להגיזרה מחפש אלמנטים כמו `app-message`, אלמנטים של `ng-icon`, ואף משתמש ב־`MutationObserver` כדי לזהות הודעות שנוספות תוך כדי עבודה. זה מתאים ל־SPA דינמי ולא לעמוד HTML סטטי שאפשר "לשאוב" בבקשה אחת. :chatgpt-content-reference{index="3"}

לכן שלוש האפשרויות הן בערך:

**WebView2** — הכי גרוע עבורך. Chromium שלם בשביל כמה שורות טקסט; הרבה RAM, תהליכים נוספים, JS, DOM, תמונות.

**HTML scraping** — קצת קל יותר אבל שביר. שינוי CSS או component באתר ויש לך תקלה.

**JSON API** — בדיוק מה שצריך. בקשת HTTP קטנה, `System.Text.Json`, יצירת Adaptive Card, וסיימנו.

הייתי בוחר בשלישי ללא היסוס.

יש עוד פרט יפה: Channel Center מנסה direct fetch ואז שרשרת של CORS proxies. זה הגיוני כי הוא אתר שרץ בדפדפן. **אל תעתיק את זה.** תוכנת Win32 עם `HttpClient` אינה כפופה ל־CORS של דפדפן, ולכן ה־Widget צריך לדבר ישירות עם הגיזרה. אין שום סיבה להעביר חדשות דרך CorsProxy/AllOrigins וכדומה.

---

# אפשר לבנות את זה כמעט על השלד שכבר כתבת

בדקתי בפועל את `NetFreeWidget`, לא רק את ה־README. [NetFreeWidget](https://github.com/Y-PLONI/NetFreeWidget)

הארכיטקטורה שלך כבר מתאימה מאוד:

`Core` → API ולוגיקה  
`BoardWidgetProvider` → COM + `IWidgetProvider`  
`Package` → MSIX/manifest

ב־`WidgetProvider.cs` שלך כבר קיימים `CreateWidget`, `Activate`, `Deactivate`, שינוי גודל, Refresh, Customize ו־Adaptive Cards. [WidgetProvider.cs](https://github.com/Y-PLONI/NetFreeWidget/blob/main/NetFreeBoardWidgetProvider/WidgetProvider.cs)

גם ה־HTTP אצלך כבר בנוי בכיוון שאני רוצה: `HttpClient` משותף, automatic decompression, connection pooling מוגבל, timeout של 10 שניות והשלכת socket idle אחרי זמן קצר. [NetFreeApi.cs](https://github.com/Y-PLONI/NetFreeWidget/blob/main/NetFreeWidget.Core/NetFreeApi.cs)

ו־`UsageService` שלך כבר מכיל רעיון מצוין שצריך להעתיק: request משותף לכל הווידג'טים, cache, מניעת בקשות מקבילות ו־backoff לאחר כישלונות. [UsageService.cs](https://github.com/Y-PLONI/NetFreeWidget/blob/main/NetFreeWidget.Core/UsageService.cs)

למעשה הייתי **לא מתחיל פרויקט ארכיטקטוני חדש בכלל**. הייתי עושה אח קרוב של NetFreeWidget.

הזרימה תהיה:

**Hagizra API → NewsService → cache מנורמל → WidgetProvider → Adaptive Card**

ובצד, רק אם המשתמש מדליק התראות:

**Notification watcher → Hagizra API → זיהוי ID חדש → Windows App Notification**

---

# דבר אחד הייתי משנה לעומת NetFreeWidget

ב־NetFreeWidget ההגדרות משותפות לכל מופעי הווידג'ט. זה הגיוני: חבילת הגלישה שלך היא אותה חבילה.

כאן הייתי מפריד מראש בין **הגדרות גלובליות** לבין **הגדרות של מופע Widget**.

גלובלי יהיו למשל: התראות מופעלות/כבויות, תדירות, האם להציג מדיה, אולי התנהגות פתיחה.

לכל Widget בנפרד יכולה להיות בעתיד קטגוריה. למשל משתמש יוכל להצמיד Widget אחד של "הכול", ואחר של קטגוריה מסוימת. ה־API כבר תומך ב־`category`, ולכן חבל לנעול את הארכיטקטורה.

Windows גם מספק `CustomState` עבור Widget, כך שאין צורך להמציא מנגנון מסורבל. Microsoft מציינת שאפשר לשמור state של הווידג'ט ולהחזיר אותו בעדכונים. :chatgpt-content-reference{index="8"}

---

# איך הייתי מעצב את הווידג'ט

כאן יש מלכודת: **לא כדאי לחקות את האתר בתוך כרטיס קטן**.

Windows Widgets אינם feed קטן. אין בהם גלילה אנכית או אופקית, ו־Microsoft אפילו קובעת שמומלץ עד touch target אחד ב־Small, עד 3 ב־Medium ועד 4 ב־Large. :chatgpt-content-reference{index="9"}

לכן על בסיס צילום המסך שלך, הייתי עושה כך:

### Small

לוגו/שם "הגיזרה", הזמן של הידיעה האחרונה, וידיעה אחת בלבד ב־3–5 שורות. כל שטח התוכן הוא לחיץ ופותח את הגיזרה.

בלי Refresh גלוי ובלי Settings גלוי. Microsoft ממליצה שב־Small תהיה פיסת מידע מרכזית אחת ו־touch target אחד. :chatgpt-content-reference{index="10"}

### Medium — זה יהיה ה־Widget העיקרי

זה הגודל שהייתי משקיע בו הכי הרבה.

בחלק העליון: `הגיזרה` ומשמאל/בקצה זמן העדכון האחרון או כפתור רענון קטן.

מתחת: **3–4 מבזקים אחרונים**, בלי כרטיס אפור נפרד סביב כל מבזק. הכרטיסים שבתוך הכרטיס מבזבזים יותר מדי שטח.

לכל שורה:

**12:43** · משפט ראשון של המבזק  
שורה שנייה אם יש צורך.

הידיעה החדשה ביותר יכולה לקבל עד 3 שורות; הבאות אחריה 2 שורות. בין ידיעות מפריד דק מאוד.

אם יש תמונה/וידאו/אודיו: רק סימן קטן `תמונה`, `וידאו` או אייקון. **לא להוריד את המדיה בכלל.**

ב־Windows ההמלצה הרשמית ל־Body היא 14px/20px, וזה גם בערך המקום שבו הייתי נשאר לטקסט החדשות; זמן יכול להיות Caption של 12px. :chatgpt-content-reference{index="11"}

### Large

6–8 ידיעות, ואם בעתיד תרצה — רק הידיעה העליונה יכולה לקבל thumbnail. לא הייתי מכניס reactions ו־views כברירת מחדל; בצילום המסך של הגיזרה הם נחמדים באתר, אבל בווידג'ט הם אוכלים בדיוק את המקום שאתה רוצה להשאיר לטקסט.

Microsoft עצמה מתארת Widget כמידע "glanceable", לא כתחליף לאתר, וממליצה שהמידע יתרחב ככל שעוברים מ־Small ל־Medium ול־Large, אך שהמטרה תישאר זהה. :chatgpt-content-reference{index="12"}

---

# עוד החלטה: טקסט קודם, מדיה אחר כך

הייתי מוציא ב־v1 לחלוטין תמונות, וידאו ואודיו מהתצוגה של Medium.

זה מרוויח בבת אחת:

פחות רשת, פחות parsing, פחות cache, אין decoding לתמונות, אין שינויי layout, ואין מצב שסרטון או תמונה הופכים Widget חדשות קטן למפלצת.

ה־Chrome extension של הגיזרה כבר מטפל באודיו, וידאו, Markdown, HTML והתראות, ואף מציע "מבזקים בזמן אמת". בחנות Chrome הוא מופיע כיום כגרסה 2.9, עודכן ב־8 ביוני 2026 וגודלו 339 KiB. :chatgpt-content-reference{index="13"}

זה גם מלמד אותנו משהו חשוב לגבי הטקסט: התוסף מציין במפורש תמיכה ב־Markdown וב־HTML. לכן **אסור לקחת `text` ולתת לו להתפרש כ־HTML חופשי**. הייתי עושה normalizer שממיר רק מה שאנחנו רוצים — שורות, אולי bold וקישורים מוכרים — וכל השאר יוצג כטקסט.

---

# ההתראות הן החלק היחיד שדורש החלטה ארכיטקטונית אמיתית

Widget Provider לבדו **אינו מנגנון התראות בזמן אמת**.

Microsoft קוראת ל־`Activate` כאשר ה־Widget host מעוניין כרגע בתוכן עדכני, ו־`Deactivate` כשהוא מפסיק לבקש אותו. היא אף מזהירה שהחלון בין השניים עשוי להיות קצר. :chatgpt-content-reference{index="14"}

כלומר אם לוח ה־Widgets סגור, אין טעם לצפות שה־provider שלך יישאר daemon שיבדוק חדשות.

יש למעשה שלושה מצבים הגיוניים:

| מצב | RAM/CPU ברקע | זמן תגובה | תשתית |
|---|---|---|---|
| התראות כבויות | כמעט אין פעילות בכלל | בעת פתיחת ה־Widget | כלום |
| Poller מקומי | process קטן ממתין רוב הזמן | למשל עד דקה | ללא שרת שלך |
| WNS Push | כמעט אפס polling במחשב | כמעט מיידי | שרת + WNS/Azure |

**לגרסה הראשונה הייתי עושה Poller מקומי אופציונלי.**

הוא בכלל לא עולה אם Notifications כבוי. כשהמשתמש מדליק התראות, הוא נרשם כ־Startup Task ורץ בכניסה ל־Windows. Windows תומך ב־StartupTask עבור desktop app ארוז ב־MSIX, והמשתמש עדיין יכול לכבות אותו ב־Task Manager/Settings. :chatgpt-content-reference{index="15"}

ה־poller לא צריך "לעבוד" כל הזמן. רוב חייו הוא `await`/sleep. פעם ב־60 שניות בקשה של 5–10 הודעות, השוואת IDs, וחזרה לשינה.

לא הייתי מעתיק את 15 השניות של Channel Center. ל־Widget שמותקן אצל הרבה משתמשים זה אגרסיבי ללא צורך. **60 שניות כבר נותנות תחושה כמעט מיידית בלי להכפיל סתם את העומס על הגיזרה.**

---

# ובשלב מתקדם יש פתרון אפילו יותר טוב

אם הפרויקט יתפוס ויהיו מאות/אלפי התקנות, אפשר לעבור ל־WNS Push.

Microsoft מייעדת Push במפורש לדברים כמו חדשות דחופות, ו־raw push יכול להעיר את היישום במקום להשאיר אותו רץ ברקע. :chatgpt-content-reference{index="16"}

אז שרת קטן אחד שלך בודק את הגיזרה פעם אחת, ומפיץ Push לכל המחשבים. במקום 1,000 מחשבים שפונים כל דקה ל־Hagizra, יש request אחד של השרת.

החיסרון: Windows App SDK Push דורש Azure/Entra registration, וב־MSIX production גם מיפוי PFN מול Microsoft. :chatgpt-content-reference{index="17"}

לכן **לא הייתי מסבך את v1 עם WNS**. אבל כן הייתי מפריד מלכתחילה `INewsNotifier`/לוגיקת notifications מה־Widget provider, כדי שתוכל לעבור מ־polling ל־push בלי לגעת ב־UI.

---

# איך ההתראות עצמן צריכות להתנהג

יש כאן כמה פרטים קטנים שיחסכו הרבה עצבים למשתמש.

כאשר המשתמש מפעיל Notifications בפעם הראשונה, מבצעים fetch ומסמנים את הידיעה החדשה ביותר כ־baseline — **לא** שולחים לפתע 10 התראות על חדשות שכבר היו.

אם מגיעים שלושה מבזקים בתוך כמה שניות, לא שולחים שלושה Toasts ברצף. מאגדים לחלון קצר ומציגים, למשל, "3 מבזקים חדשים מהגיזרה".

אם המחשב ישן שעתיים וחוזר ויש 28 חדשות, לא מציפים את Action Center. שולחים הודעה מסכמת אחת.

Windows App SDK מספק היום `AppNotificationManager` ליישומי .NET, כולל התראה מקומית והפעלת האפליקציה/פעולה כאשר לוחצים עליה. :chatgpt-content-reference{index="18"}

כיבוי ההתראות אצלך צריך להיות **ברירת המחדל**, כפי שביקשת, וכשהן כבויות אין סיבה להפעיל בכלל את ה־poller.

---

# מנגנון העדכון של ה־Widget עצמו

כאן הייתי משפר מעט לעומת NetFreeWidget.

כאשר לוח Widgets נפתח: אם ה־cache צעיר מ־60–90 שניות, מציגים אותו מיד בלי רשת. אם הוא ישן יותר, מציגים מיד את הקיים ובמקביל מבקשים חדש.

אם כמה Widget instances מופעלים יחד, כולם מחכים ל־Task אחד משותף — בדיוק כמו `_inflight` שכבר עשית ב־UsageService.

אם אין רשת, לא צריך להציג כרטיס ריק. ממשיכים להראות את החדשות האחרונות שנשמרו בדיסק עם caption קטן "עודכן 11:42".

אחרי כישלון: 30 שניות → דקה → 2 → 5 → 15 דקות. הרעיון הזה כבר קיים אצלך.

וכאשר החדשות מתעדכנות, **אין צורך לשלוח מחדש את Template כולו**. Microsoft מאפשרת לעדכן רק `Data` ולהשאיר את ה־Adaptive Card template הקיים, וזה בדיוק מתאים ל־feed כזה. :chatgpt-content-reference{index="19"}

זה גם מפחית allocations ועבודה מיותרת.

---

# הייתי שומר cache קטן בדיסק

לא database.

קובץ JSON קטן עם, נניח, 30–50 הודעות אחרונות, ועוד metadata:

`lastFetch`  
`lastSeenId`  
`lastNotifiedId`  
`ETag` אם השרת מספק  
אולי hash של התוכן

זה הכול.

את ה־settings אפשר להשאיר בצורה דומה לזו שכבר קיימת אצלך. את cache החדשות הייתי שם ב־LocalAppData ולא ב־Roaming AppData.

חשוב גם לא להניח ש־`id` לבדו הוא מספר מונוטוני. לצורכי "חדש" הייתי שומר recent-ID set קטן ומשווה גם timestamp. אם אותה הודעה נערכה אבל ה־ID זהה, מעדכנים אותה בווידג'ט — ולא שולחים התראה חדשה.

---

# ETag ו־HTTP caching: צריך לבדוק ולא לנחש

ב־Channel Center מצאתי שהם מוסיפים timestamp לבקשה כדי לעקוף caching בדפדפן.

**אני לא הייתי מעתיק את זה ל־Windows.**

ב־native client עדיף לבדוק מה הגיזרה שולחת:

אם יש `ETag`, שלח `If-None-Match` וקבל `304` בלי JSON.

אם יש `Last-Modified`, אפשר להשתמש בו.

אם אין כלום, פשוט fetch קטן של `limit=5` או `limit=10`.

הוספת `?t=Date.now()` לכל בקשה הורסת בכוח כל אפשרות caching, וזה מנוגד למטרה שלך של תוכנה חסכונית.

---

# לגבי התהליך עצמו ו־RAM — מצאתי אצלך נקודה מעניינת

ב־`Program.cs` של NetFreeWidget ה־COM server יושב על `ExitEvent.WaitOne()`, וה־event משתחרר רק כשנמחק ה־Widget האחרון. כלומר המודל שלך היום הוא "אין עבודת רקע", אבל **התהליך לא בהכרח נסגר רק מפני שלוח ה־Widgets נסגר**. [Program.cs](https://github.com/Y-PLONI/NetFreeWidget/blob/main/NetFreeBoardWidgetProvider/Program.cs)

Microsoft מבהירה ש־Widget Provider הוא Out-of-process COM server ושה־lifetime שלו עצמאי מחיי ה־Widget; Widget Manager רשאי להפעיל ולשחרר אותו. :chatgpt-content-reference{index="21"}

לכן ב־HagizraWidget, שבו חשוב לך מאוד RAM, הייתי **מודד שתי גרסאות**:

האחת — אותו lifetime כמו NetFreeWidget, שהוא הכי פשוט ובטוח.

השנייה — יציאה של ה־provider כאשר אין אף Widget פעיל, תוך הסתמכות על COM activation שיפעיל אותו שוב. זה יכול לתת למעשה 0 working-set כשהלוח סגור וההתראות כבויות, אבל הייתי מאמת התנהגות על כמה builds של Windows 11 לפני שמאמץ זאת. לא הייתי משנה את NetFreeWidget הקיים על סמך התיאוריה בלבד.

---

# מה לא הייתי מכניס ל־v1

לא WebView2.

לא HTML scraper.

לא תמונות אוטומטיות.

לא וידאו.

לא reactions.

לא מנגנון login.

לא SQLite.

לא polling של 15 שניות.

לא proxy חיצוני.

לא שירות Windows שרץ תמיד.

לא הייתי אפילו מציג author בכל ידיעה אם ברוב המקרים הוא פשוט "הגיזרה" — זה מידע שחוזר על עצמו וגוזל שורה.

---

# ומה כן הייתי מכניס ל־v1

הייתי בונה אותו בארבעה שלבים בלבד:

1. **HagizraApiClient + NewsNormalizer** — אימות חי של `/api/messages`, `/api/categories`, ה־schema, headers ומקרי media; שמירת JSON גולמי אחד כ־fixture לבדיקות.
2. **Widget בלי התראות** — Small/Medium/Large, cache, refresh, RTL, dark/light, ללא מדיה. זה כבר מוצר שימושי מאוד.
3. **Notification watcher אופציונלי** — כבוי כברירת מחדל, 60 שניות, dedupe, burst grouping ו־backoff.
4. **אופטימיזציה לאחר מדידה** — RAM אמיתי ב־Task Manager/Process Explorer, זמן activation, JSON bytes/day, ואז רק אם צריך לשנות lifetime או לעבור ל־WNS.

---

## מבחינתי הארכיטקטורה הסופית הנכונה

**אותה טכנולוגיה שכבר יש לך: C#/.NET + Windows App SDK + Adaptive Cards + MSIX.**

לא הייתי עובר ל־C++, Rust, WinUI GUI או משהו אחר רק בשביל לחסוך כמה MB. כבר יש לך infrastructure שעובד, installer, manifest ו־Widget provider. Windows עצמו מציג Widgets באמצעות Adaptive Cards ו־packaged Win32 provider, כך שאתה בדיוק במסלול שמיקרוסופט מיועדת לו. :chatgpt-content-reference{index="22"}

החידוש העיקרי הוא לא טכנולוגי אלא ארכיטקטוני:

**API ישיר + feed טקסטואלי צפוף וחכם + cache + process אופציונלי נפרד להתראות.**

והנקודה שלדעתי הכי חשובה: בגלל שאין גלילה ב־Windows Widgets, לא הייתי מנסה לייצר "הגיזרה בתוך Widget". הייתי מייצר **חלון הצצה של הגיזרה** — שלוש־ארבע החדשות שבאמת אפשר לקרוא בנוחות בגודל שצירפת, ואז לחיצה אחת לאתר. זה גם ייראה הרבה יותר טוב וגם יהיה הרבה יותר קל.

אם הייתי מעביר עכשיו מפרט לסוכן שמממש את זה, הייתי כבר מספיק בטוח בארכיטקטורה הזאת כדי להתחיל — ורק דורש ממנו כשלב ראשון **ללכוד ולתעד response חי אמיתי של שלושת endpoints של Hagizra לפני כתיבת ה־DTOs**.