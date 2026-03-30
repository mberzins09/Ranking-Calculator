// See https://aka.ms/new-console-template for more information
using RankingCalculator.Data;
using RankingCalculator.Logic;

//Database database = new Database();
//RankingEngine rankingEngine = new RankingEngine(database);
//rankingEngine.Run();

Database database = new Database();
RankingEngine2 rankingEngine = new RankingEngine2(database);
rankingEngine.Run("rankings3");


//EloCalculator elo = new EloCalculator();

//int r1 = 2000;
//int r2 = 1500;

//double e1 = elo.Expected(r1, r2);
//double e2 = elo.Expected(r2, r1);

//int p1 = elo.NewRating(r1, 1, e1, 0.5);
//int p2 = elo.NewRating(r2, 0, e2, 0.5);

//int p3 = elo.NewRating(r1, 0, e1, 0.5);
//int p4 = elo.NewRating(r2, 1, e2, 0.5);

//Console.WriteLine($"Stronger wins {p1 - r1}, Weaker wins {p4 - r2}");

//Database database = new Database();
//await database.ImportAll();

//database.CleanDuplicateGames();

//AddInitialPoints help = new AddInitialPoints();
//await help.AddInitialPointsToSource();
