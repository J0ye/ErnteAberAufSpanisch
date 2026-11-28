namespace Cosecha.Tests
{
    /// <summary>Sample vocabulary (schema 2), so tests do not depend on the live word list.</summary>
    public static class TestVocabulary
    {
        public const string Json = @"{
  ""version"": 2,
  ""entries"": [
    { ""id"": ""yo"", ""es"": ""yo"", ""de"": [""ich""], ""type"": ""pronoun"", ""pronoun"": { ""person"": 1, ""number"": ""sg"", ""gender"": ""x"" } },
    { ""id"": ""tu"", ""es"": ""tú"", ""de"": [""du""], ""type"": ""pronoun"", ""pronoun"": { ""person"": 2, ""number"": ""sg"", ""gender"": ""x"" } },
    { ""id"": ""el_pron"", ""es"": ""él"", ""de"": [""er""], ""type"": ""pronoun"", ""pronoun"": { ""person"": 3, ""number"": ""sg"", ""gender"": ""m"" } },
    { ""id"": ""el"", ""es"": ""el"", ""de"": [""der"", ""das""], ""type"": ""article"", ""article"": { ""definite"": true, ""gender"": ""m"", ""number"": ""sg"" } },
    { ""id"": ""la"", ""es"": ""la"", ""de"": [""die""], ""type"": ""article"", ""article"": { ""definite"": true, ""gender"": ""f"", ""number"": ""sg"" } },
    { ""id"": ""un"", ""es"": ""un"", ""de"": [""ein""], ""type"": ""article"", ""article"": { ""definite"": false, ""gender"": ""m"", ""number"": ""sg"" } },
    { ""id"": ""y"", ""es"": ""y"", ""de"": [""und""], ""type"": ""conjunction"" },
    { ""id"": ""que"", ""es"": ""qué"", ""de"": [""was""], ""type"": ""questionWord"", ""questionWord"": { ""role"": ""object"" } },
    { ""id"": ""quien"", ""es"": ""quién"", ""de"": [""wer""], ""type"": ""questionWord"", ""questionWord"": { ""role"": ""subject"" } },
    { ""id"": ""donde"", ""es"": ""dónde"", ""de"": [""wo""], ""type"": ""questionWord"", ""questionWord"": { ""role"": ""adverb"" } },
    { ""id"": ""comer"", ""es"": ""comer"", ""de"": [""essen""], ""type"": ""verb"",
      ""verb"": { ""irregular"": false, ""transitive"": true, ""copula"": false,
        ""present"": { ""yo"": ""como"", ""tu"": ""comes"", ""el"": ""come"", ""nosotros"": ""comemos"", ""vosotros"": ""coméis"", ""ellos"": ""comen"" } } },
    { ""id"": ""vivir"", ""es"": ""vivir"", ""de"": [""leben"", ""wohnen""], ""type"": ""verb"",
      ""verb"": { ""irregular"": false, ""transitive"": false, ""copula"": false,
        ""present"": { ""yo"": ""vivo"", ""tu"": ""vives"", ""el"": ""vive"", ""nosotros"": ""vivimos"", ""vosotros"": ""vivís"", ""ellos"": ""viven"" } } },
    { ""id"": ""ser"", ""es"": ""ser"", ""de"": [""sein""], ""type"": ""verb"",
      ""verb"": { ""irregular"": true, ""transitive"": false, ""copula"": true,
        ""present"": { ""yo"": ""soy"", ""tu"": ""eres"", ""el"": ""es"", ""nosotros"": ""somos"", ""vosotros"": ""sois"", ""ellos"": ""son"" } } },
    { ""id"": ""pan"", ""es"": ""pan"", ""de"": [""Brot""], ""type"": ""noun"",
      ""noun"": { ""gender"": ""m"", ""number"": ""sg"", ""articleDefinite"": ""el"", ""articleIndefinite"": ""un"" } },
    { ""id"": ""casa"", ""es"": ""casa"", ""de"": [""Haus""], ""type"": ""noun"",
      ""noun"": { ""gender"": ""f"", ""number"": ""sg"", ""articleDefinite"": ""la"", ""articleIndefinite"": ""una"" } },
    { ""id"": ""perro"", ""es"": ""perro"", ""de"": [""Hund""], ""type"": ""noun"",
      ""noun"": { ""gender"": ""m"", ""number"": ""sg"", ""articleDefinite"": ""el"", ""articleIndefinite"": ""un"" } },
    { ""id"": ""gato"", ""es"": ""gato"", ""de"": [""Katze"", ""Kater""], ""type"": ""noun"",
      ""noun"": { ""gender"": ""m"", ""number"": ""sg"", ""articleDefinite"": ""el"", ""articleIndefinite"": ""un"" } },
    { ""id"": ""agua"", ""es"": ""agua"", ""de"": [""Wasser""], ""type"": ""noun"",
      ""noun"": { ""gender"": ""f"", ""number"": ""sg"", ""articleDefinite"": ""el"", ""articleIndefinite"": ""un"" } },
    { ""id"": ""grande"", ""es"": ""grande"", ""de"": [""groß"", ""gross""], ""type"": ""adjective"",
      ""adjective"": { ""forms"": { ""msg"": ""grande"", ""fsg"": ""grande"", ""mpl"": ""grandes"", ""fpl"": ""grandes"" } } },
    { ""id"": ""rojo"", ""es"": ""rojo"", ""de"": [""rot""], ""type"": ""adjective"",
      ""adjective"": { ""forms"": { ""msg"": ""rojo"", ""fsg"": ""roja"", ""mpl"": ""rojos"", ""fpl"": ""rojas"" } } },
    { ""id"": ""siempre"", ""es"": ""siempre"", ""de"": [""immer""], ""type"": ""other"" },
    { ""id"": ""buenos_dias"", ""es"": ""buenos días"", ""de"": [""guten Morgen"", ""guten Tag""], ""type"": ""phrase"" }
  ]
}";
    }
}
