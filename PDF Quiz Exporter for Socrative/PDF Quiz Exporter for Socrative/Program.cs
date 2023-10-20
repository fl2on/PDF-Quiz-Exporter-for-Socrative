using iTextSharp.text;
using iTextSharp.text.pdf;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using Image = iTextSharp.text.Image;

namespace PDF_Quiz_Exporter_for_Socrative
{
    internal abstract class Program
    {
        private static void Main()
        {
            var roomName = GetInput("Enter the name of the classroom: ").ToLower();
            var resObj = GetResponse($"https://api.socrative.com/rooms/api/current-activity/{roomName}");

            if (resObj.Count > 0)
            {
                var activityId = resObj["activity_id"]?.ToString();
                var resObj1 = GetResponse($"https://teacher.socrative.com/quizzes/{activityId}/student?room={roomName}",
                    "SA_0AFd_7NneDk0WafSUS0u0fIkHIJFmz3X");
                DisplayQuizInfo(resObj1);

                ExportQuizInfoToPdf(resObj1, "QuizInfo.pdf");

                Console.WriteLine("\nQuiz information exported to QuizInfo.pdf.");
            }
            else
            {
                Console.WriteLine("\nInactive classroom");
            }

            DisplayLoadingAnimation();

            Console.ReadLine();
        }

        private static string GetInput(string prompt)
        {
            Console.Write(prompt);
            return Console.ReadLine();
        }

        private static void DisplayLoadingAnimation()
        {
            var counter = 0;
            while (true)
            {
                switch (counter % 4)
                {
                    case 0:
                        Console.Write("/");
                        break;
                    case 1:
                        Console.Write("-");
                        break;
                    case 2:
                        Console.Write("\\");
                        break;
                    case 3:
                        Console.Write("|");
                        break;
                }

                Thread.Sleep(100);
                if (Console.KeyAvailable)
                    break;
                counter++;
                Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
            }
        }

        private static void DisplayQuizInfo(JObject quizInfo)
        {
            Console.WriteLine($"\nName: {quizInfo["name"]}");
            Console.WriteLine($"Id: {quizInfo["activity_id"]}");
            Console.WriteLine($"Number of questions: {quizInfo["questions"].Count()}");

            for (var i = 0; i < quizInfo["questions"].Count(); i++)
            {
                var question = quizInfo["questions"][i];
                Console.WriteLine($"\nQuestion {i + 1}: {question?["question_text"]}");

                if (question?["question_image"] != null && question["question_image"].HasValues)
                    Console.WriteLine($"(Img url: {question["question_image"]["url"]})");

                switch (question?["type"]?.ToString())
                {
                    case "MC":
                    case "TF":
                        for (var j = 0; j < (question["answers"] ?? throw new InvalidOperationException()).Count(); j++)
                        {
                            var answer = question["answers"][j];
                            Console.WriteLine($"{j + 1}) {answer?["text"]} ({answer?["id"]})");
                        }

                        break;
                    case "FR":
                        Console.WriteLine(" - Free response");
                        break;
                }
            }
        }

        private static void ExportQuizInfoToPdf(JObject quizInfo, string pdfFilePath)
        {
            using (var document = new Document())
            {
                PdfWriter.GetInstance(document, new FileStream(pdfFilePath, FileMode.Create));
                document.Open();

                var questionFont = FontFactory.GetFont("Arial", 12, Font.BOLD);
                var answerFont = FontFactory.GetFont("Arial", 12, Font.NORMAL);

                for (var i = 0; i < quizInfo["questions"].Count(); i++)
                {
                    var question = quizInfo["questions"][i];
                    document.Add(new Paragraph(
                        $"\nQuestion {i + 1}: {CleanText(question?["question_text"]?.ToString())}", questionFont));

                    if (question?["question_image"] != null && question["question_image"].HasValues)
                    {
                        var image = Image.GetInstance(new Uri(question["question_image"]["url"]?.ToString() ??
                                                              string.Empty));
                        document.Add(image);
                    }

                    switch (question?["type"]?.ToString())
                    {
                        case "MC":
                        case "TF":
                            for (var j = 0;
                                 j < (question["answers"] ?? throw new InvalidOperationException()).Count();
                                 j++)
                            {
                                var answer = question["answers"][j];
                                document.Add(new Paragraph($"{j + 1}) {CleanText(answer?["text"]?.ToString())}",
                                    answerFont));
                            }

                            break;
                        case "FR":
                            document.Add(new Paragraph(" - Free response", answerFont));
                            break;
                    }
                }
            }
        }

        private static JObject GetResponse(string url, string cookieValue = null)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";

            if (!string.IsNullOrEmpty(cookieValue))
            {
                request.CookieContainer = new CookieContainer();
                request.CookieContainer.Add(new Cookie("sa", cookieValue, "/", "teacher.socrative.com"));
            }

            var response = (HttpWebResponse)request.GetResponse();
            var resStream = response.GetResponseStream();
            var reader = new StreamReader(resStream ?? throw new InvalidOperationException());
            var resJson = reader.ReadToEnd();

            return JObject.Parse(resJson);
        }

        private static string CleanText(string text)
        {
            text = Regex.Replace(text, @"<[^>]*>", string.Empty);
            return text;
        }
    }
}