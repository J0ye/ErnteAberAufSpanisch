namespace Cosecha.Tests
{
    /// <summary>The sample vocabulary from the design document (section 14), so tests do not depend on the live word list.</summary>
    public static class TestVocabulary
    {
        public const string Json = @"{
  ""version"": 1,
  ""entries"": [
    { ""id"": ""casa"", ""es"": ""casa"", ""de"": [""Haus""], ""type"": ""noun"",
      ""noun"": { ""gender"": ""f"", ""articleDefinite"": ""la"", ""articleIndefinite"": ""una"" }, ""tags"": [""A1""] },
    { ""id"": ""perro"", ""es"": ""perro"", ""de"": [""Hund""], ""type"": ""noun"",
      ""noun"": { ""gender"": ""m"", ""articleDefinite"": ""el"", ""articleIndefinite"": ""un"" }, ""tags"": [""A1""] },
    { ""id"": ""agua"", ""es"": ""agua"", ""de"": [""Wasser""], ""type"": ""noun"",
      ""noun"": { ""gender"": ""f"", ""articleDefinite"": ""el"", ""articleIndefinite"": ""un"" }, ""tags"": [""A1""] },
    { ""id"": ""informacion"", ""es"": ""información"", ""de"": [""Information"", ""Auskunft""], ""type"": ""noun"",
      ""noun"": { ""gender"": ""f"", ""articleDefinite"": ""la"", ""articleIndefinite"": ""una"" }, ""tags"": [""A2""] },
    { ""id"": ""comer"", ""es"": ""comer"", ""de"": [""essen""], ""type"": ""verb"",
      ""verb"": { ""irregular"": false, ""present"": { ""yo"": ""como"", ""tu"": ""comes"", ""el"": ""come"", ""nosotros"": ""comemos"", ""vosotros"": ""coméis"", ""ellos"": ""comen"" } }, ""tags"": [""A1""] },
    { ""id"": ""hablar"", ""es"": ""hablar"", ""de"": [""sprechen"", ""reden""], ""type"": ""verb"",
      ""verb"": { ""irregular"": false, ""present"": { ""yo"": ""hablo"", ""tu"": ""hablas"", ""el"": ""habla"", ""nosotros"": ""hablamos"", ""vosotros"": ""habláis"", ""ellos"": ""hablan"" } }, ""tags"": [""A1""] },
    { ""id"": ""ser"", ""es"": ""ser"", ""de"": [""sein""], ""type"": ""verb"",
      ""verb"": { ""irregular"": true, ""present"": { ""yo"": ""soy"", ""tu"": ""eres"", ""el"": ""es"", ""nosotros"": ""somos"", ""vosotros"": ""sois"", ""ellos"": ""son"" } }, ""tags"": [""A1""] },
    { ""id"": ""tener"", ""es"": ""tener"", ""de"": [""haben""], ""type"": ""verb"",
      ""verb"": { ""irregular"": true, ""present"": { ""yo"": ""tengo"", ""tu"": ""tienes"", ""el"": ""tiene"", ""nosotros"": ""tenemos"", ""vosotros"": ""tenéis"", ""ellos"": ""tienen"" } }, ""tags"": [""A1""] },
    { ""id"": ""grande"", ""es"": ""grande"", ""de"": [""groß"", ""gross""], ""type"": ""adjective"", ""tags"": [""A1""] },
    { ""id"": ""rojo"", ""es"": ""rojo"", ""de"": [""rot""], ""type"": ""adjective"", ""tags"": [""A1""] },
    { ""id"": ""pequeno"", ""es"": ""pequeño"", ""de"": [""klein""], ""type"": ""adjective"", ""tags"": [""A1""] },
    { ""id"": ""dos"", ""es"": ""dos"", ""de"": [""zwei""], ""type"": ""other"", ""tags"": [""A1"", ""Zahl""] },
    { ""id"": ""siempre"", ""es"": ""siempre"", ""de"": [""immer""], ""type"": ""other"", ""tags"": [""A1""] },
    { ""id"": ""manana"", ""es"": ""mañana"", ""de"": [""morgen""], ""type"": ""other"", ""tags"": [""A1""] },
    { ""id"": ""que"", ""es"": ""qué"", ""de"": [""was""], ""type"": ""other"", ""tags"": [""A1"", ""Fragewort""] },
    { ""id"": ""buenos_dias"", ""es"": ""buenos días"", ""de"": [""guten Morgen"", ""guten Tag""], ""type"": ""phrase"", ""tags"": [""A1""] },
    { ""id"": ""que_tal"", ""es"": ""¿qué tal?"", ""de"": [""wie geht's"", ""wie geht es dir""], ""type"": ""phrase"", ""tags"": [""A1""] }
  ]
}";
    }
}
