using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;
using System.Diagnostics;
using System.Numerics;
using System.Collections.Generic;

namespace Star_War_List
{
    public static class Program
    {
        private static readonly HttpClient client = new HttpClient();
        private static string swapi = "https://swapi.dev/api";

        private static string selfPlanetsPath = "/planets";
        private static string selfFavoritesPath = "/favorites";

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddDbContext<FavoritePlanetDb>(opt => opt.UseInMemoryDatabase("FavoritePlanets"));
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();
            var app = builder.Build();

            var planets = app.MapGroup(selfPlanetsPath);
            planets.MapGet("/", GetPlanets);

            var favorites = planets.MapGroup(selfFavoritesPath);
            favorites.MapGet("/", GetFavoritePlanets);
            favorites.MapGet("/{id}", GetFavoritePlanet);
            favorites.MapPost("/{id}", PostFavoritePlanet);
            favorites.MapDelete("/{id}", DeleteFavoritePlanet);

            app.Run();
        }

        private static string targetPlanetPath = $"/planets";
        private static string targetPlanetEndpoint = $"{swapi}{targetPlanetPath}";
        private static string targetArrayFieldName = "results";
        private static string targetNextFieldName = "next";

        private static async Task<IResult> GetPlanets()
        {
            var target = targetPlanetEndpoint;
            var list = new List<JsonNode?>();

            do
            {
                var response = await client.GetAsync(target);
                if (!response.IsSuccessStatusCode)
                    break;

                var jsonDocument = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                if (!jsonDocument.RootElement.TryGetProperty(targetArrayFieldName, out JsonElement jsonPlanetsElement))
                    break;

                if (jsonPlanetsElement.ValueKind != JsonValueKind.Array)
                    break;
                var jsonArray = JsonArray.Create(jsonPlanetsElement);

                if (jsonArray is null)
                    break;
                list = list.Concat(jsonArray).ToList();
                jsonArray.Clear();

                if (!jsonDocument.RootElement.TryGetProperty(targetNextFieldName, out JsonElement jsonNextElement))
                    break;

                if (jsonNextElement.ValueKind != JsonValueKind.String)
                    break;
                target = jsonNextElement.GetString();
            }
            while (true);

            var planets = new JsonArray(list.ToArray());
            return TypedResults.Ok(planets);
        }
        private static async Task<JsonNode?> GetPlanetDetails(FavoritePlanet planet)
        {
            var target = $"{targetPlanetEndpoint}/{planet.Id}";
            var response = await client.GetAsync(target);
            if (!response.IsSuccessStatusCode)
                return null;

            var jsonDocument = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            if (jsonDocument is null)
                return null;

            return JsonObject.Create(jsonDocument.RootElement);
        }

        private static async Task<IResult> GetFavoritePlanets(FavoritePlanetDb db)
        {
            return TypedResults.Ok(await db.FavoritePlanets.Select(planet => GetPlanetDetails(planet).Result).ToListAsync());
        }

        private static async Task<IResult> GetFavoritePlanet(int id, FavoritePlanetDb db)
        {
            return await db.FavoritePlanets.FindAsync(id) is FavoritePlanet planet ? 
                TypedResults.Ok(await GetPlanetDetails(planet)) : 
                TypedResults.NotFound();
        }

        private static async Task<IResult> PostFavoritePlanet(int id, FavoritePlanetDb db)
        {
            var response = await client.GetAsync($"{targetPlanetEndpoint}/{id}");
            if (!response.IsSuccessStatusCode)
                return TypedResults.BadRequest($"{id} provided not found on {targetPlanetEndpoint}, or {targetPlanetEndpoint} is down");

            db.FavoritePlanets.Add(new FavoritePlanet { Id = id });
            await db.SaveChangesAsync();

            return TypedResults.Created($"{selfPlanetsPath}{selfFavoritesPath}/{id}", id);
        }

        private static async Task<IResult> DeleteFavoritePlanet(int id, FavoritePlanetDb db)
        {
            if (await db.FavoritePlanets.FindAsync(id) is FavoritePlanet planet)
            {
                db.FavoritePlanets.Remove(planet);
                await db.SaveChangesAsync();

                var details = await GetPlanetDetails(planet);
                if (details is null)
                    return Results.NoContent();

                return Results.Ok(details);
            }

            return Results.NotFound();
        }
    }
}
