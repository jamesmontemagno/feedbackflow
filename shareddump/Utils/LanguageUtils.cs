namespace SharedDump.Utils;

public static class LanguageUtils
{
    // Static readonly dictionary that is initialized only once
    private static readonly Dictionary<string, string> _languageMap;

    // Static constructor to initialize the dictionary once when the class is first used
    static LanguageUtils()
    {
        // Initialize the language map dictionary
        _languageMap = new Dictionary<string, string>
        {
            // English variants
            {"en-US", "English (United States)"},
            {"en-GB", "English (United Kingdom)"},
            {"en-AU", "English (Australia)"},
            {"en-CA", "English (Canada)"},
            {"en-IN", "English (India)"},
            {"en-IE", "English (Ireland)"},
            {"en-NZ", "English (New Zealand)"},
            {"en-HK", "English (Hong Kong SAR)"},
            {"en-SG", "English (Singapore)"},
            {"en-ZA", "English (South Africa)"},
            {"en-KE", "English (Kenya)"},
            {"en-NG", "English (Nigeria)"},
            {"en-PH", "English (Philippines)"},
            {"en-TZ", "English (Tanzania)"},
            {"en", "English"},
            
            // Spanish variants
            {"es-ES", "Spanish (Spain)"},
            {"es-MX", "Spanish (Mexico)"},
            {"es-AR", "Spanish (Argentina)"},
            {"es-CO", "Spanish (Colombia)"},
            {"es-CL", "Spanish (Chile)"},
            {"es-PE", "Spanish (Peru)"},
            {"es-VE", "Spanish (Venezuela)"},
            {"es-US", "Spanish (United States)"},
            {"es-DO", "Spanish (Dominican Republic)"},
            {"es-GT", "Spanish (Guatemala)"},
            {"es-CR", "Spanish (Costa Rica)"},
            {"es-HN", "Spanish (Honduras)"},
            {"es-NI", "Spanish (Nicaragua)"},
            {"es-BO", "Spanish (Bolivia)"},
            {"es-EC", "Spanish (Ecuador)"},
            {"es-PR", "Spanish (Puerto Rico)"},
            {"es-PA", "Spanish (Panama)"},
            {"es-UY", "Spanish (Uruguay)"},
            {"es-PY", "Spanish (Paraguay)"},
            {"es-SV", "Spanish (El Salvador)"},
            {"es-CU", "Spanish (Cuba)"},
            {"es-GQ", "Spanish (Equatorial Guinea)"},
            {"es", "Spanish"},
            
            // French variants
            {"fr-FR", "French (France)"},
            {"fr-CA", "French (Canada)"},
            {"fr-BE", "French (Belgium)"},
            {"fr-CH", "French (Switzerland)"},
            {"fr", "French"},
            
            // German variants
            {"de-DE", "German (Germany)"},
            {"de-AT", "German (Austria)"},
            {"de-CH", "German (Switzerland)"},
            {"de", "German"},
            
            // Chinese variants
            {"zh-CN", "Chinese (Mainland)"},
            {"zh-TW", "Chinese (Taiwan)"},
            {"zh-HK", "Chinese (Hong Kong SAR)"},
            {"zh", "Chinese"},
            
            // Portuguese variants
            {"pt-BR", "Portuguese (Brazil)"},
            {"pt-PT", "Portuguese (Portugal)"},
            {"pt", "Portuguese"},
            
            // Arabic variants
            {"ar-SA", "Arabic (Saudi Arabia)"},
            {"ar-AE", "Arabic (United Arab Emirates)"},
            {"ar-EG", "Arabic (Egypt)"},
            {"ar-KW", "Arabic (Kuwait)"},
            {"ar-MA", "Arabic (Morocco)"},
            {"ar-DZ", "Arabic (Algeria)"},
            {"ar-BH", "Arabic (Bahrain)"},
            {"ar-IQ", "Arabic (Iraq)"},
            {"ar-JO", "Arabic (Jordan)"},
            {"ar-LB", "Arabic (Lebanon)"},
            {"ar-LY", "Arabic (Libya)"},
            {"ar-OM", "Arabic (Oman)"},
            {"ar-QA", "Arabic (Qatar)"},
            {"ar-SY", "Arabic (Syria)"},
            {"ar-TN", "Arabic (Tunisia)"},
            {"ar-YE", "Arabic (Yemen)"},
            {"ar", "Arabic"},
            
            // Other languages
            {"af-ZA", "Afrikaans (South Africa)"},
            {"am-ET", "Amharic (Ethiopia)"},
            {"az-AZ", "Azerbaijani (Azerbaijan)"},
            {"bg-BG", "Bulgarian (Bulgaria)"},
            {"bn-BD", "Bangla (Bangladesh)"},
            {"bn-IN", "Bengali (India)"},
            {"bs-BA", "Bosnian (Bosnia)"},
            {"ca", "Catalan"},
            {"cs-CZ", "Czech (Czech Republic)"},
            {"cy-GB", "Welsh (United Kingdom)"},
            {"da-DK", "Danish (Denmark)"},
            {"el-GR", "Greek (Greece)"},
            {"et-EE", "Estonian (Estonia)"},
            {"fi-FI", "Finnish (Finland)"},
            {"fil-PH", "Filipino (Philippines)"},
            {"ga-IE", "Irish (Ireland)"},
            {"gl", "Galician"},
            {"gu-IN", "Gujarati (India)"},
            {"he-IL", "Hebrew (Israel)"},
            {"hi-IN", "Hindi (India)"},
            {"hr-HR", "Croatian (Croatia)"},
            {"hu-HU", "Hungarian (Hungary)"},
            {"hy-AM", "Armenian (Armenia)"},
            {"id-ID", "Indonesian (Indonesia)"},
            {"is-IS", "Icelandic (Iceland)"},
            {"it-IT", "Italian (Italy)"},
            {"ja-JP", "Japanese (Japan)"},
            {"jv-ID", "Javanese (Indonesia)"},
            {"ka-GE", "Georgian (Georgia)"},
            {"kk-KZ", "Kazakh (Kazakhstan)"},
            {"km-KH", "Khmer (Cambodia)"},
            {"kn-IN", "Kannada (India)"},
            {"ko-KR", "Korean (South Korea)"},
            {"lo-LA", "Lao (Laos)"},
            {"lt-LT", "Lithuanian (Lithuania)"},
            {"lv-LV", "Latvian (Latvia)"},
            {"mk-MK", "Macedonian (North Macedonia)"},
            {"ml-IN", "Malayalam (India)"},
            {"mr-IN", "Marathi (India)"},
            {"ms-MY", "Malay (Malaysia)"},
            {"mt-MT", "Maltese (Malta)"},
            {"my-MM", "Burmese (Myanmar)"},
            {"nb-NO", "Norwegian Bokmål (Norway)"},
            {"ne-NP", "Nepali (Nepal)"},
            {"nl-BE", "Dutch (Belgium)"},
            {"nl-NL", "Dutch (Netherlands)"},
            {"pl-PL", "Polish (Poland)"},
            {"ps-AF", "Pashto (Afghanistan)"},
            {"ro-RO", "Romanian (Romania)"},
            {"ru-RU", "Russian (Russia)"},
            {"si-LK", "Sinhala (Sri Lanka)"},
            {"sk-SK", "Slovak (Slovakia)"},
            {"sl-SI", "Slovenian (Slovenia)"},
            {"so-SO", "Somali (Somalia)"},
            {"sq-AL", "Albanian (Albania)"},
            {"sr-RS", "Serbian (Serbia)"},
            {"su-ID", "Sundanese (Indonesia)"},
            {"sv-SE", "Swedish (Sweden)"},
            {"sw-KE", "Swahili (Kenya)"},
            {"sw-TZ", "Swahili (Tanzania)"},
            {"ta-IN", "Tamil (India)"},
            {"ta-LK", "Tamil (Sri Lanka)"},
            {"ta-MY", "Tamil (Malaysia)"},
            {"ta-SG", "Tamil (Singapore)"},
            {"te-IN", "Telugu (India)"},
            {"th-TH", "Thai (Thailand)"},
            {"tr-TR", "Turkish (Türkiye)"},
            {"uk-UA", "Ukrainian (Ukraine)"},
            {"ur-IN", "Urdu (India)"},
            {"ur-PK", "Urdu (Pakistan)"},
            {"uz-UZ", "Uzbek (Uzbekistan)"},            {"vi-VN", "Vietnamese (Vietnam)"},
            {"zu-ZA", "Zulu (South Africa)"}
        };
    }

    /// <summary>
    /// Converts a language code to a readable language name
    /// </summary>
    /// <param name="langCode">The language code (e.g., "en-US")</param>
    /// <returns>The readable language name (e.g., "English (United States)")</returns>
    public static string GetLanguageName(string? langCode)
    {
        return _languageMap.TryGetValue(langCode ?? "", out var name) ? name : langCode ?? "";
    }
}
