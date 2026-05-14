using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Text;
using System.Linq;

namespace RavnLearnWeb.Controllers
{
    public class ChatController : Controller
    {
        private bool IsLoggedIn() => HttpContext.Session.GetString("Username") != null;
        private int UserId => HttpContext.Session.GetInt32("UserId") ?? 0;
        private string Username => HttpContext.Session.GetString("Username") ?? "";

        public IActionResult Index()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            ViewBag.Username = Username;
            ViewBag.UserInitial = Username.Length > 0 ? Username[0].ToString().ToUpper() : "?";
            return View();
        }

        public IActionResult MyChats()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            ViewBag.Username    = Username;
            ViewBag.Email       = HttpContext.Session.GetString("Email") ?? "";
            ViewBag.UserInitial = Username.Length > 0 ? Username[0].ToString().ToUpper() : "?";
            return View();
        }

        public async Task<IActionResult> Flashcards()
{
    if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
    ViewBag.Username    = Username;
    ViewBag.UserInitial = Username.Length > 0 ? Username[0].ToString().ToUpper() : "?";

    var projects = new List<dynamic>();
    try
    {
        using var conn = RavnLearnWeb.Database.GetConnection();
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand(
            @"SELECT p.project_id,
                     p.name,
                     COALESCE(p.subject, '')         AS subject,
                     p.created_at,
                     COUNT(DISTINCT fc.flashcard_id) AS card_count
              FROM projects p
              LEFT JOIN chats c        ON c.project_id = p.project_id
              LEFT JOIN flashcards fc  ON fc.chat_id   = c.chat_id
              WHERE p.user_id = @uid
              GROUP BY p.project_id
              ORDER BY p.created_at DESC", conn);
        cmd.Parameters.AddWithValue("uid", UserId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            projects.Add(new
            {
                Id        = reader.GetInt32(0),
                Name      = reader.GetString(1),
                Subject   = reader.GetString(2),
                Date      = reader.GetDateTime(3),
                CardCount = reader.GetInt64(4)
            });
        }
    }
    catch { }

    ViewBag.Flashcards = projects;
    return View();
}  

        public IActionResult Quizzes()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            ViewBag.Username    = Username;
            ViewBag.Email       = HttpContext.Session.GetString("Email") ?? "";
            ViewBag.UserInitial = Username.Length > 0 ? Username[0].ToString().ToUpper() : "?";
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetChats()
        {
            if (!IsLoggedIn()) return Unauthorized();
            var chats = new List<object>();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    @"SELECT chat_id, COALESCE(name, 'New Chat') AS title 
                    FROM chats WHERE user_id = @uid 
                    ORDER BY created_at DESC", conn);
                cmd.Parameters.AddWithValue("uid", UserId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    chats.Add(new { id = reader.GetInt32(0), title = reader.GetString(1) });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
            return Json(chats);
        }

        [HttpPost]
        public async Task<IActionResult> NewChat()
        {
            if (!IsLoggedIn()) return Unauthorized();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    "INSERT INTO chats (user_id, name, created_at) VALUES (@uid, @name, @now) RETURNING chat_id", conn);
                cmd.Parameters.AddWithValue("uid", UserId);
                cmd.Parameters.AddWithValue("name", "New Chat");
                cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
                int newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return Json(new { id = newId, title = "New Chat" });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> CreateChat([FromBody] CreateChatRequest req)
        {
            if (!IsLoggedIn()) return Unauthorized();
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { error = "Name is required" });

            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    @"INSERT INTO chats (user_id, name, subject, created_at)
                    VALUES (@uid, @name, @subject, @now)
                    RETURNING chat_id", conn);
                cmd.Parameters.AddWithValue("uid", UserId);
                cmd.Parameters.AddWithValue("name", req.Name);
                cmd.Parameters.AddWithValue("subject", req.Subject ?? "");
                cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
                int newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return Json(new { id = newId, name = req.Name, subject = req.Subject ?? "" });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet]
        public async Task<IActionResult> GetMyChats()
        {
            if (!IsLoggedIn()) return Unauthorized();
            var chats = new List<object>();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    @"SELECT c.chat_id,
                            COALESCE(c.name, 'New Chat') AS name,
                            COALESCE(c.subject, '')      AS subject,
                            c.created_at,
                            COUNT(f.file_id)             AS file_count
                    FROM chats c
                    LEFT JOIN files f ON f.chat_id = c.chat_id
                    LEFT JOIN projects p ON p.project_id = c.project_id
                    WHERE c.user_id = @uid 
                    AND (c.is_manual IS NULL OR c.is_manual = false)
                    AND (p.is_manual IS NULL OR p.is_manual = false)
                    GROUP BY c.chat_id
                    ORDER BY c.created_at DESC", conn);
                cmd.Parameters.AddWithValue("uid", UserId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    chats.Add(new
                    {
                        id        = reader.GetInt32(0),
                        name      = reader.GetString(1),
                        subject   = reader.GetString(2),
                        date      = reader.GetDateTime(3).ToString("MMM dd, yyyy"),
                        fileCount = reader.GetInt64(4)
                    });
                }
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
            return Json(chats);
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteChat(int id)
        {
            if (!IsLoggedIn()) return Unauthorized();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();

                using var cmdF = new NpgsqlCommand("DELETE FROM files WHERE chat_id = @cid", conn);
                cmdF.Parameters.AddWithValue("cid", id);
                await cmdF.ExecuteNonQueryAsync();

                using var cmdQ = new NpgsqlCommand(
                    @"DELETE FROM quizzes WHERE session_id IN 
                    (SELECT session_id FROM chat_sessions WHERE chat_id = @cid)", conn);
                cmdQ.Parameters.AddWithValue("cid", id);
                await cmdQ.ExecuteNonQueryAsync();

                using var cmdFc = new NpgsqlCommand(
                    @"DELETE FROM flashcards WHERE session_id IN 
                    (SELECT session_id FROM chat_sessions WHERE chat_id = @cid)", conn);
                cmdFc.Parameters.AddWithValue("cid", id);
                await cmdFc.ExecuteNonQueryAsync();

                using var cmdSess = new NpgsqlCommand(
                    "DELETE FROM chat_sessions WHERE chat_id = @cid", conn);
                cmdSess.Parameters.AddWithValue("cid", id);
                await cmdSess.ExecuteNonQueryAsync();

                using var cmd0 = new NpgsqlCommand("DELETE FROM chat_documents WHERE chat_id = @cid", conn);
                cmd0.Parameters.AddWithValue("cid", id);
                await cmd0.ExecuteNonQueryAsync();

                using var cmd1 = new NpgsqlCommand("DELETE FROM messages WHERE chat_id = @cid", conn);
                cmd1.Parameters.AddWithValue("cid", id);
                await cmd1.ExecuteNonQueryAsync();

                using var cmd2 = new NpgsqlCommand(
                    "DELETE FROM chats WHERE chat_id = @cid AND user_id = @uid", conn);
                cmd2.Parameters.AddWithValue("cid", id);
                cmd2.Parameters.AddWithValue("uid", UserId);
                await cmd2.ExecuteNonQueryAsync();

                return Json(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> RenameChat([FromBody] RenameChatRequest req)
        {
            if (!IsLoggedIn()) return Unauthorized();
            if (req.ChatId <= 0 || string.IsNullOrWhiteSpace(req.Title))
                return BadRequest(new { error = "Chat title is required." });

            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    @"UPDATE chats
                      SET name = @title
                      WHERE chat_id = @cid AND user_id = @uid", conn);
                cmd.Parameters.AddWithValue("title", req.Title.Trim());
                cmd.Parameters.AddWithValue("cid", req.ChatId);
                cmd.Parameters.AddWithValue("uid", UserId);

                int rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0) return NotFound(new { error = "Chat not found." });

                return Json(new { success = true, title = req.Title.Trim() });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet]
        public async Task<IActionResult> GetMessages(int chatId)
        {
            if (!IsLoggedIn()) return Unauthorized();
            var messages = new List<object>();
            var docFilenames = new List<string>();
            string chatTitle = "New Chat";

            try
            {
                using (var conn = RavnLearnWeb.Database.GetConnection())
                {
                    await conn.OpenAsync();
                    using var cmd = new NpgsqlCommand(
                       "SELECT COALESCE(name, 'New Chat') FROM chats WHERE chat_id = @cid", conn);
                    cmd.Parameters.AddWithValue("cid", chatId);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                        chatTitle = result.ToString() ?? "New Chat";
                }

                using (var conn = RavnLearnWeb.Database.GetConnection())
                {
                    await conn.OpenAsync();
                    using var cmd = new NpgsqlCommand(
                        "SELECT filename FROM files WHERE chat_id = @cid ORDER BY uploaded_at ASC", conn);
                    cmd.Parameters.AddWithValue("cid", chatId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                        docFilenames.Add(reader.GetString(0));
                }

                using (var conn = RavnLearnWeb.Database.GetConnection())
                {
                    await conn.OpenAsync();
                    using var cmd = new NpgsqlCommand(
                        "SELECT role, content FROM messages WHERE chat_id = @cid ORDER BY timestamp ASC", conn);
                    cmd.Parameters.AddWithValue("cid", chatId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                        messages.Add(new { role = reader.GetString(0), content = reader.GetString(1) });
                }
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }

            return Json(new
            {
                messages,
                docFilenames,
                docFilename = docFilenames.FirstOrDefault(),
                chatTitle
            });
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest req)
        {
            if (!IsLoggedIn()) return Unauthorized();
            if (string.IsNullOrWhiteSpace(req.Message))
                return BadRequest(new { error = "Empty message" });

            try
            {
                int chatId = req.ChatId;

                if (chatId <= 0)
                {
                    using var conn = RavnLearnWeb.Database.GetConnection();
                    await conn.OpenAsync();
                    using var newCmd = new NpgsqlCommand(
                        "INSERT INTO chats (user_id, name, created_at) VALUES (@uid, @name, @now) RETURNING chat_id", conn);
                    newCmd.Parameters.AddWithValue("uid", UserId);
                    newCmd.Parameters.AddWithValue("name", "New Chat");
                    newCmd.Parameters.AddWithValue("now", DateTime.UtcNow);
                    chatId = Convert.ToInt32(await newCmd.ExecuteScalarAsync());
                }

                string newTitle = "";
                using (var conn = RavnLearnWeb.Database.GetConnection())
                {
                    await conn.OpenAsync();
                    using var countCmd = new NpgsqlCommand(
                        "SELECT COUNT(*) FROM messages WHERE chat_id = @cid AND role = 'user'", conn);
                    countCmd.Parameters.AddWithValue("cid", chatId);
                    long count = (long)(await countCmd.ExecuteScalarAsync() ?? 0L);

                    if (count == 0)
                    {
                        newTitle = req.Message.Length > 40
                            ? req.Message.Substring(0, 40) + "…"
                            : req.Message;
                        using var titleCmd = new NpgsqlCommand(
                            "UPDATE chats SET name = @t WHERE chat_id = @cid", conn);
                        titleCmd.Parameters.AddWithValue("t", newTitle);
                        titleCmd.Parameters.AddWithValue("cid", chatId);
                        await titleCmd.ExecuteNonQueryAsync();
                    }
                }

                var docContextBuilder = new StringBuilder();
                using (var conn = RavnLearnWeb.Database.GetConnection())
                {
                    await conn.OpenAsync();
                    using var dcmd = new NpgsqlCommand(
                         "SELECT filename, extracted_text FROM files WHERE chat_id = @cid ORDER BY uploaded_at ASC", conn);
                    dcmd.Parameters.AddWithValue("cid", chatId);
                    using var dr = await dcmd.ExecuteReaderAsync();
                    while (await dr.ReadAsync())
                    {
                        docContextBuilder.AppendLine($"=== Document: {dr.GetString(0)} ===");
                        docContextBuilder.AppendLine(dr.GetString(1));
                        docContextBuilder.AppendLine();
                    }
                }

                await SaveMessage(chatId, "user", req.Message);
                string reply = await GeminiService.AskAsync(req.Message, docContextBuilder.ToString());
                await SaveMessage(chatId, "ai", reply);

                return Json(new { reply, chatId, newTitle });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> UploadDocument(IFormFile file, int chatId)
        {
            if (!IsLoggedIn()) return Unauthorized();
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided" });

            string ext = Path.GetExtension(file.FileName).ToLower();
            if (ext != ".pdf" && ext != ".docx" && ext != ".txt")
                return BadRequest(new { error = "Only PDF, DOCX, and TXT files are supported." });

            try
            {
                if (chatId <= 0)
                {
                    using var conn0 = RavnLearnWeb.Database.GetConnection();
                    await conn0.OpenAsync();
                    using var nc = new NpgsqlCommand(
                        "INSERT INTO chats (user_id, name, created_at) VALUES (@uid, @name, @now) RETURNING chat_id", conn0);
                    nc.Parameters.AddWithValue("uid", UserId);
                    nc.Parameters.AddWithValue("name", "New Chat");
                    nc.Parameters.AddWithValue("now", DateTime.UtcNow);
                    chatId = Convert.ToInt32(await nc.ExecuteScalarAsync());
                }

                string fileName = file.FileName;
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                byte[] fileBytes = ms.ToArray();

                string docText = ext == ".txt" ? Encoding.UTF8.GetString(fileBytes)
                               : ext == ".pdf" ? ExtractPdfText(fileBytes)
                               : ExtractDocxText(fileBytes);

                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    @"INSERT INTO files (chat_id, filename, extracted_text, uploaded_at)
                    VALUES (@cid, @fn, @dt, @now)", conn);
                cmd.Parameters.AddWithValue("cid", chatId);
                cmd.Parameters.AddWithValue("fn", fileName);
                cmd.Parameters.AddWithValue("dt", docText);
                cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
                await cmd.ExecuteNonQueryAsync();

                string welcome = $"Document \"{fileName}\" loaded! You can ask me questions about it, request a summary, or say \"create a quiz\".";
                await SaveMessage(chatId, "ai", welcome);

                return Json(new { success = true, chatId, fileName, welcome });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> SaveQuiz([FromBody] SaveQuizRequest req)
        {
            if (!IsLoggedIn()) return Unauthorized();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    "INSERT INTO quizzes (chat_id, quiz_text, created_at) VALUES (@cid, @qt, @now) RETURNING quiz_id", conn);
                cmd.Parameters.AddWithValue("cid", req.ChatId);
                cmd.Parameters.AddWithValue("qt", req.QuizText);
                cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
                int newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return Json(new { success = true, quizId = newId });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet]
        public async Task<IActionResult> GetQuizzes(int chatId)
        {
            if (!IsLoggedIn()) return Unauthorized();
            var quizzes = new List<object>();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    "SELECT quiz_id, quiz_text FROM quizzes WHERE chat_id = @cid ORDER BY created_at ASC", conn);
                cmd.Parameters.AddWithValue("cid", chatId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    quizzes.Add(new { id = reader.GetInt32(0), text = reader.GetString(1) });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
            return Json(quizzes);
        }

        [HttpPost]
        public async Task<IActionResult> SaveQuizQuestions([FromBody] SaveQuizQuestionsRequest req)
        {
            if (!IsLoggedIn()) return Unauthorized();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();

                // Step 1: Create a chat_session
                using var sessionCmd = new NpgsqlCommand(
                    @"INSERT INTO chat_sessions (chat_id, title, created_at)
                    VALUES (@cid, @title, @now)
                    RETURNING session_id", conn);
                sessionCmd.Parameters.AddWithValue("cid", req.ChatId);
                var pst = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Asia/Manila");
                sessionCmd.Parameters.AddWithValue("title", $"Session {pst:MMM dd, h:mm tt}");
                sessionCmd.Parameters.AddWithValue("now", DateTime.UtcNow);
                int sessionId = Convert.ToInt32(await sessionCmd.ExecuteScalarAsync());

                // Step 2: Save each question to quizzes AND flashcards
                foreach (var q in req.Questions)
                {
                    // ── Save to quizzes (unchanged) ──
                    using var quizCmd = new NpgsqlCommand(
                        @"INSERT INTO quizzes 
                            (chat_id, session_id, question, question_type,
                            choice_a, choice_b, choice_c, choice_d,
                            correct_answer, answer_text, created_at)
                        VALUES 
                            (@cid, @sid, @q, @qtype,
                            @a, @b, @c, @d,
                            @ans, @atxt, @now)", conn);
                    quizCmd.Parameters.AddWithValue("cid",   req.ChatId);
                    quizCmd.Parameters.AddWithValue("sid",   sessionId);
                    quizCmd.Parameters.AddWithValue("q",     q.Question);
                    quizCmd.Parameters.AddWithValue("qtype", q.QuestionType);
                    quizCmd.Parameters.AddWithValue("a",     q.ChoiceA);
                    quizCmd.Parameters.AddWithValue("b",     q.ChoiceB);
                    quizCmd.Parameters.AddWithValue("c",     q.ChoiceC);
                    quizCmd.Parameters.AddWithValue("d",     q.ChoiceD);
                    quizCmd.Parameters.AddWithValue("ans",   q.CorrectAnswer);
                    quizCmd.Parameters.AddWithValue("atxt",  q.AnswerText);
                    quizCmd.Parameters.AddWithValue("now",   DateTime.UtcNow);
                    await quizCmd.ExecuteNonQueryAsync();

                    // ── Resolve the correct answer text for the flashcard back ──
                    // flashcards.back = the full answer text, not just the letter
                    string back = q.QuestionType switch
                    {
                        "fillblank" => q.AnswerText,
                        "truefalse" => q.CorrectAnswer.ToUpper() == "A" ? "True" : "False",
                        _           => q.CorrectAnswer.ToUpper() switch  // mcq default
                        {
                            "A" => q.ChoiceA,
                            "B" => q.ChoiceB,
                            "C" => q.ChoiceC,
                            "D" => q.ChoiceD,
                            _   => q.CorrectAnswer
                        }
                    };

                    // ── Save to flashcards ──
                    // Schema: flashcards(flashcard_id, chat_id, front, back, created_at, session_id)
                    using var fcCmd = new NpgsqlCommand(
                        @"INSERT INTO flashcards (chat_id, session_id, front, back, created_at)
                        VALUES (@cid, @sid, @front, @back, @now)", conn);
                    fcCmd.Parameters.AddWithValue("cid",   req.ChatId);
                    fcCmd.Parameters.AddWithValue("sid",   sessionId);
                    fcCmd.Parameters.AddWithValue("front", q.Question);
                    fcCmd.Parameters.AddWithValue("back",  back);
                    fcCmd.Parameters.AddWithValue("now",   DateTime.UtcNow);
                    await fcCmd.ExecuteNonQueryAsync();
                }

                return Json(new { success = true, sessionId });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }
      
        [HttpGet]
        public async Task<IActionResult> GetQuizzesByChat(int chatId)
        {
            if (!IsLoggedIn()) return Unauthorized();
            var questions = new List<object>();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    @"SELECT q.quiz_id, q.question, 
                            COALESCE(q.question_type, 'mcq') AS question_type,
                            q.choice_a, q.choice_b, q.choice_c, q.choice_d, 
                            q.correct_answer, COALESCE(q.answer_text,'') AS answer_text,
                            q.session_id,
                            COALESCE(cs.title, 'Reviewer') AS session_title,
                            cs.created_at
                    FROM quizzes q
                    JOIN chat_sessions cs ON cs.session_id = q.session_id
                    WHERE cs.chat_id = @cid
                    ORDER BY q.quiz_id ASC", conn);
                cmd.Parameters.AddWithValue("cid", chatId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    questions.Add(new {
                        id            = reader.GetInt32(0),
                        question      = reader.GetString(1),
                        questionType  = reader.GetString(2),
                        choiceA       = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        choiceB       = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        choiceC       = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        choiceD       = reader.IsDBNull(6) ? "" : reader.GetString(6),
                        correctAnswer = reader.IsDBNull(7) ? "" : reader.GetString(7),
                        answerText    = reader.GetString(8),
                        sessionId     = reader.GetInt32(9),
                        sessionTitle  = reader.GetString(10),
                        createdAt     = reader.GetDateTime(11).ToString("MMM dd")
                    });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
            return Json(questions);
        }

        [HttpGet]
        public async Task<IActionResult> GetChatDocuments(int chatId)
        {
            if (!IsLoggedIn()) return Unauthorized();

            var docs = new List<object>();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    @"SELECT f.file_id,
                             f.filename,
                             COALESCE(f.extracted_text, '') AS extracted_text
                      FROM files f
                      JOIN chats c ON c.chat_id = f.chat_id
                      WHERE f.chat_id = @cid AND c.user_id = @uid
                      ORDER BY f.uploaded_at ASC", conn);
                cmd.Parameters.AddWithValue("cid", chatId);
                cmd.Parameters.AddWithValue("uid", UserId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    docs.Add(new
                    {
                        id = reader.GetInt32(0),
                        filename = reader.GetString(1),
                        content = reader.GetString(2)
                    });
                }
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }

            return Json(docs);
        }

        [HttpPost]
        public async Task<IActionResult> SaveFlashcards([FromBody] SaveFlashcardsRequest req)
        {
            if (!IsLoggedIn()) return Unauthorized();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                foreach (var card in req.Cards)
                {
                    using var cmd = new NpgsqlCommand(
                        @"INSERT INTO flashcards (chat_id, front, back, created_at)
                        VALUES (@cid, @front, @back, @now)", conn);
                    cmd.Parameters.AddWithValue("cid",   req.ChatId);
                    cmd.Parameters.AddWithValue("front", card.Front);
                    cmd.Parameters.AddWithValue("back",  card.Back);
                    cmd.Parameters.AddWithValue("now",   DateTime.UtcNow);
                    await cmd.ExecuteNonQueryAsync();
                }
                return Json(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet]
        public async Task<IActionResult> GetFlashcardsByChat(int chatId)
        {
            if (!IsLoggedIn()) return Unauthorized();
            var cards = new List<object>();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    @"SELECT flashcard_id, front, back FROM flashcards
                    WHERE chat_id = @cid ORDER BY created_at ASC", conn);
                cmd.Parameters.AddWithValue("cid", chatId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    cards.Add(new {
                        id    = reader.GetInt32(0),
                        front = reader.GetString(1),
                        back  = reader.GetString(2)
                    });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
            return Json(cards);
        }

        [HttpPost]
        public async Task<IActionResult> NewSession([FromBody] NewSessionRequest req)
        {
            if (!IsLoggedIn()) return Unauthorized();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();

                using var check = new NpgsqlCommand(
                    "SELECT 1 FROM projects WHERE project_id = @pid AND user_id = @uid", conn);
                check.Parameters.AddWithValue("pid", req.FolderId);
                check.Parameters.AddWithValue("uid", UserId);
                if (await check.ExecuteScalarAsync() == null) return Forbid();

                using var chatCmd = new NpgsqlCommand(
                    @"INSERT INTO chats (user_id, project_id, name, created_at)
                    VALUES (@uid, @pid, 'New Chat', @now)
                    RETURNING chat_id", conn);
                chatCmd.Parameters.AddWithValue("uid", UserId);
                chatCmd.Parameters.AddWithValue("pid", req.FolderId);
                chatCmd.Parameters.AddWithValue("now", DateTime.UtcNow);
                int chatId = Convert.ToInt32(await chatCmd.ExecuteScalarAsync());

                using var cmd = new NpgsqlCommand(
                    @"INSERT INTO chat_sessions (chat_id, title, created_at)
                    VALUES (@cid, 'New Chat', @now)
                    RETURNING session_id", conn);
                cmd.Parameters.AddWithValue("cid", chatId);
                cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
                int newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return Json(new { id = newId, title = "New Chat" });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet]
        public async Task<IActionResult> GetSessions(int folderId)
        {
            if (!IsLoggedIn()) return Unauthorized();
            var sessions = new List<object>();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    @"SELECT s.session_id, s.title
                    FROM chat_sessions s
                    JOIN chats c ON c.chat_id = s.chat_id
                    JOIN projects p ON p.project_id = c.project_id
                    WHERE p.project_id = @pid AND p.user_id = @uid
                    ORDER BY s.created_at DESC", conn);
                cmd.Parameters.AddWithValue("pid", folderId);
                cmd.Parameters.AddWithValue("uid", UserId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    sessions.Add(new { id = reader.GetInt32(0), title = reader.GetString(1) });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
            return Json(sessions);
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteSession(int id)
        {
            if (!IsLoggedIn()) return Unauthorized();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                foreach (var tbl in new[] { "files", "quizzes", "flashcards", "messages" })
                {
                    using var del = new NpgsqlCommand(
                        $"DELETE FROM {tbl} WHERE session_id = @sid", conn);
                    del.Parameters.AddWithValue("sid", id);
                    await del.ExecuteNonQueryAsync();
                }
                using var cmd = new NpgsqlCommand(
                    @"DELETE FROM chat_sessions s
                    USING chats c
                    WHERE s.session_id = @sid AND s.chat_id = c.chat_id AND c.user_id = @uid", conn);
                cmd.Parameters.AddWithValue("sid", id);
                cmd.Parameters.AddWithValue("uid", UserId);
                await cmd.ExecuteNonQueryAsync();
                return Json(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> RenameSession([FromBody] RenameSessionRequest req)
        {
            if (!IsLoggedIn()) return Unauthorized();
            if (string.IsNullOrWhiteSpace(req.Title))
                return BadRequest(new { error = "Title is required" });

            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    @"UPDATE chat_sessions s SET title = @title
                      FROM chats c
                      WHERE s.session_id = @sid AND s.chat_id = c.chat_id AND c.user_id = @uid", conn);
                cmd.Parameters.AddWithValue("title", req.Title);
                cmd.Parameters.AddWithValue("sid", req.Id);
                cmd.Parameters.AddWithValue("uid", UserId);
                int rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0) return NotFound(new { error = "Session not found" });
                return Json(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> RenameProject([FromBody] RenameProjectRequest req)
        {
            if (!IsLoggedIn()) return Unauthorized();
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { error = "Name is required" });

            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    "UPDATE projects SET name = @name WHERE project_id = @pid AND user_id = @uid", conn);
                cmd.Parameters.AddWithValue("name", req.Name);
                cmd.Parameters.AddWithValue("pid", req.Id);
                cmd.Parameters.AddWithValue("uid", UserId);
                int rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0) return NotFound(new { error = "Project not found" });
                return Json(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteProject(int id)
        {
            if (!IsLoggedIn()) return Unauthorized();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();

                var chatIds = new List<int>();
                using (var cmd = new NpgsqlCommand(
                    "SELECT chat_id FROM chats WHERE project_id = @pid AND user_id = @uid", conn))
                {
                    cmd.Parameters.AddWithValue("pid", id);
                    cmd.Parameters.AddWithValue("uid", UserId);
                    using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync()) chatIds.Add(r.GetInt32(0));
                }

                foreach (var cid in chatIds)
                {
                    using var cmdF = new NpgsqlCommand("DELETE FROM files WHERE chat_id = @cid", conn);
                    cmdF.Parameters.AddWithValue("cid", cid);
                    await cmdF.ExecuteNonQueryAsync();

                    using var cmdQ = new NpgsqlCommand(
                        "DELETE FROM quizzes WHERE session_id IN (SELECT session_id FROM chat_sessions WHERE chat_id = @cid)", conn);
                    cmdQ.Parameters.AddWithValue("cid", cid);
                    await cmdQ.ExecuteNonQueryAsync();

                    using var cmdFc = new NpgsqlCommand("DELETE FROM flashcards WHERE chat_id = @cid", conn);
                    cmdFc.Parameters.AddWithValue("cid", cid);
                    await cmdFc.ExecuteNonQueryAsync();

                    using var cmdSess = new NpgsqlCommand("DELETE FROM chat_sessions WHERE chat_id = @cid", conn);
                    cmdSess.Parameters.AddWithValue("cid", cid);
                    await cmdSess.ExecuteNonQueryAsync();

                    using var cmdM = new NpgsqlCommand("DELETE FROM messages WHERE chat_id = @cid", conn);
                    cmdM.Parameters.AddWithValue("cid", cid);
                    await cmdM.ExecuteNonQueryAsync();

                    using var cmdC = new NpgsqlCommand("DELETE FROM chats WHERE chat_id = @cid", conn);
                    cmdC.Parameters.AddWithValue("cid", cid);
                    await cmdC.ExecuteNonQueryAsync();
                }

                using var cmdP = new NpgsqlCommand(
                    "DELETE FROM projects WHERE project_id = @pid AND user_id = @uid", conn);
                cmdP.Parameters.AddWithValue("pid", id);
                cmdP.Parameters.AddWithValue("uid", UserId);
                await cmdP.ExecuteNonQueryAsync();

                return Json(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> CreateProject([FromBody] CreateChatRequest req)
        {
            if (!IsLoggedIn()) return Unauthorized();
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { error = "Name is required" });

            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    @"INSERT INTO projects (user_id, name, subject, created_at)
                    VALUES (@uid, @name, @subject, @now)
                    RETURNING project_id", conn);
                cmd.Parameters.AddWithValue("uid", UserId);
                cmd.Parameters.AddWithValue("name", req.Name);
                cmd.Parameters.AddWithValue("subject", req.Subject ?? "");
                cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
                int newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return Json(new { id = newId, name = req.Name, subject = req.Subject ?? "" });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet]
        public async Task<IActionResult> GetMyProjects()
        {
            if (!IsLoggedIn()) return Unauthorized();
            var projects = new List<object>();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    @"SELECT p.project_id,
                            p.name,
                            COALESCE(p.subject, '')         AS subject,
                            p.created_at,
                            COUNT(DISTINCT f.file_id)       AS file_count,
                            COUNT(DISTINCT fc.flashcard_id) AS flashcard_count,
                            COALESCE(p.is_manual, false)    AS is_manual
                    FROM projects p
                    LEFT JOIN chats c       ON c.project_id = p.project_id
                    LEFT JOIN files f       ON f.chat_id    = c.chat_id
                    LEFT JOIN flashcards fc ON fc.chat_id   = c.chat_id
                    WHERE p.user_id = @uid
                    GROUP BY p.project_id
                    ORDER BY p.created_at DESC", conn);
                cmd.Parameters.AddWithValue("uid", UserId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    projects.Add(new
                    {
                        id             = reader.GetInt32(0),
                        name           = reader.GetString(1),
                        subject        = reader.GetString(2),
                        date           = reader.GetDateTime(3).ToString("MMM dd, yyyy"),
                        fileCount      = reader.GetInt64(4),
                        flashcardCount = reader.GetInt64(5),
                        isManual       = reader.GetBoolean(6), // ← added
                    });
                }
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
            return Json(projects);
        }
            
        [HttpGet]
        public async Task<IActionResult> GetChatsInProject(int projectId)
        {
            if (!IsLoggedIn()) return Unauthorized();
            var chats = new List<object>();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    @"SELECT c.chat_id, COALESCE(c.name, 'New Chat') AS title
                    FROM chats c
                    JOIN projects p ON p.project_id = c.project_id
                    WHERE c.project_id = @pid AND p.user_id = @uid
                    ORDER BY c.created_at DESC", conn);
                cmd.Parameters.AddWithValue("pid", projectId);
                cmd.Parameters.AddWithValue("uid", UserId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    chats.Add(new { id = reader.GetInt32(0), title = reader.GetString(1) });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
            return Json(chats);
        }

        [HttpPost]
        public async Task<IActionResult> NewChatInProject([FromBody] NewSessionRequest req)
        {
            if (!IsLoggedIn()) return Unauthorized();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();

                using var check = new NpgsqlCommand(
                    "SELECT 1 FROM projects WHERE project_id = @pid AND user_id = @uid", conn);
                check.Parameters.AddWithValue("pid", req.FolderId);
                check.Parameters.AddWithValue("uid", UserId);
                if (await check.ExecuteScalarAsync() == null) return Forbid();

                using var cmd = new NpgsqlCommand(
                    @"INSERT INTO chats (user_id, project_id, name, created_at)
                    VALUES (@uid, @pid, 'New Chat', @now)
                    RETURNING chat_id", conn);
                cmd.Parameters.AddWithValue("uid", UserId);
                cmd.Parameters.AddWithValue("pid", req.FolderId);
                cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
                int newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return Json(new { id = newId, title = "New Chat" });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet]
        public async Task<IActionResult> QuizzesView(int id)
            {
                if (!IsLoggedIn()) return RedirectToAction("Login", "Account");

                string projectName = "My Quiz";
                string projectDate = "";

                try
                {
                    using var conn = RavnLearnWeb.Database.GetConnection();
                    await conn.OpenAsync();
                    using var cmd = new NpgsqlCommand(
                        "SELECT name, created_at, COALESCE(is_manual, false) FROM projects WHERE project_id = @pid AND user_id = @uid", conn);
                    cmd.Parameters.AddWithValue("pid", id);
                    cmd.Parameters.AddWithValue("uid", UserId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        projectName = reader.GetString(0);
                        projectDate = reader.GetDateTime(1).ToString("MMM dd, yyyy");
                        ViewBag.IsManual = reader.GetBoolean(2);
                    }
                }
                catch { }

                ViewBag.SetName  = projectName.ToUpper();
                ViewBag.SetDate  = projectDate;
                ViewBag.Username = Username;
                ViewBag.Email    = HttpContext.Session.GetString("Email") ?? "";
                return View();
            }

        [HttpGet]
        public async Task<IActionResult> GetQuizzesByProject(int projectId)
        {
            if (!IsLoggedIn()) return Unauthorized();
            var result = new List<object>();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(@"
                    SELECT q.quiz_id, q.question, q.choice_a, q.choice_b, q.choice_c, q.choice_d,
                        q.correct_answer, q.question_type, q.answer_text, q.session_id, q.created_at,
                        COALESCE(cs.title, '') AS session_title        -- ← ADD THIS
                    FROM quizzes q
                    JOIN chats c ON c.chat_id = q.chat_id
                    JOIN chat_sessions cs ON cs.session_id = q.session_id -- ← ADD THIS
                    WHERE c.project_id = @pid AND c.user_id = @uid
                    ORDER BY q.session_id, q.quiz_id", conn);
                cmd.Parameters.AddWithValue("pid", projectId);
                cmd.Parameters.AddWithValue("uid", UserId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new {
                        quizId        = reader.GetInt32(0),
                        question      = reader.GetString(1),
                        choiceA       = reader.IsDBNull(2)  ? "" : reader.GetString(2),
                        choiceB       = reader.IsDBNull(3)  ? "" : reader.GetString(3),
                        choiceC       = reader.IsDBNull(4)  ? "" : reader.GetString(4),
                        choiceD       = reader.IsDBNull(5)  ? "" : reader.GetString(5),
                        correctAnswer = reader.IsDBNull(6)  ? "" : reader.GetString(6),
                        questionType  = reader.IsDBNull(7)  ? "mcq" : reader.GetString(7),
                        answerText    = reader.IsDBNull(8)  ? "" : reader.GetString(8),
                        sessionId     = reader.IsDBNull(9)  ? 0   : reader.GetInt32(9),
                        createdAt     = reader.GetDateTime(10).ToString("MMM dd, yyyy"),
                        sessionTitle  = reader.GetString(11)  // ← ADD THIS
                    });
                }
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
            return Json(result);
        }
      
        [HttpGet]
        public async Task<IActionResult> FlashcardView(int id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");

            string projectName = "";
            DateTime projectDate = DateTime.UtcNow;
            var sessions = new List<dynamic>();

            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();

                using var infoCmd = new NpgsqlCommand(
                    "SELECT name, created_at FROM projects WHERE project_id = @pid AND user_id = @uid", conn);
                infoCmd.Parameters.AddWithValue("pid", id);
                infoCmd.Parameters.AddWithValue("uid", UserId);
                using var infoReader = await infoCmd.ExecuteReaderAsync();
                if (await infoReader.ReadAsync())
                {
                    projectName = infoReader.GetString(0);
                    projectDate = infoReader.GetDateTime(1);
                }
                await infoReader.CloseAsync();

                // Get sessions with flashcard count per session
                using var cmd = new NpgsqlCommand(
                    @"SELECT cs.session_id,
                            cs.title,
                            cs.created_at,
                            COUNT(fc.flashcard_id) AS card_count,
                            COUNT(fr.flashcard_id) FILTER (WHERE fr.status = 'correct') AS got_count
                    FROM chat_sessions cs
                    JOIN chats c ON c.chat_id = cs.chat_id
                    JOIN flashcards fc ON fc.session_id = cs.session_id
                    LEFT JOIN flashcard_reviews fr ON fr.flashcard_id = fc.flashcard_id AND fr.user_id = @uid
                    WHERE c.project_id = @pid
                    GROUP BY cs.session_id, cs.title, cs.created_at
                    ORDER BY cs.created_at ASC", conn);
                cmd.Parameters.AddWithValue("pid", id);
                cmd.Parameters.AddWithValue("uid", UserId);
                using var reader = await cmd.ExecuteReaderAsync();
                int idx = 1;
                while (await reader.ReadAsync())
                {
                    sessions.Add(new
                    {
                        SessionId = reader.GetInt32(0),
                        Title     = $"FLASHCARD SET {idx:D2}",
                        Date      = reader.GetDateTime(2).ToString("MMM dd, yyyy"),
                        CardCount = reader.GetInt64(3),
                        GotCount  = reader.GetInt64(4)
                    });
                    idx++;
                }
            }
            catch { }

            ViewBag.SetName      = projectName.ToUpper();
            ViewBag.SetDate      = projectDate.ToString("MMM dd, yyyy");
            ViewBag.SessionCount = sessions.Count;
            ViewBag.Sessions     = sessions;
            ViewBag.ProjectId    = id;
            ViewBag.Username     = Username;
            ViewBag.Email        = HttpContext.Session.GetString("Email") ?? "";
            return View();
        }
        
        [HttpGet]
        public async Task<IActionResult> FlashcardStudy(int sessionId)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");

            var cards = new List<dynamic>();
            string sessionTitle = "";
            string projectName  = "";
            int    projectId    = 0;

            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();

                using var infoCmd = new NpgsqlCommand(
                    @"SELECT cs.title, p.name, p.project_id
                    FROM chat_sessions cs
                    JOIN chats c ON c.chat_id = cs.chat_id
                    JOIN projects p ON p.project_id = c.project_id
                    WHERE cs.session_id = @sid AND p.user_id = @uid", conn);
                infoCmd.Parameters.AddWithValue("sid", sessionId);
                infoCmd.Parameters.AddWithValue("uid", UserId);
                using var infoReader = await infoCmd.ExecuteReaderAsync();
                if (await infoReader.ReadAsync())
                {
                    sessionTitle = infoReader.GetString(0);
                    projectName  = infoReader.GetString(1);
                    projectId    = infoReader.GetInt32(2);
                }
                await infoReader.CloseAsync();

                using var cmd = new NpgsqlCommand(
                    @"SELECT flashcard_id, front, back
                    FROM flashcards
                    WHERE session_id = @sid
                    ORDER BY created_at ASC", conn);
                cmd.Parameters.AddWithValue("sid", sessionId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    cards.Add(new
                    {
                        Id    = reader.GetInt32(0),
                        Front = reader.GetString(1),
                        Back  = reader.GetString(2)
                    });
                }
            }
            catch { }

            var cardsList  = cards.Select(c => (object)new { front = (string)c.Front, back = (string)c.Back }).ToList();
            var cardsJson  = System.Text.Json.JsonSerializer.Serialize<List<object>>(cardsList);

            ViewBag.SessionTitle = sessionTitle;
            ViewBag.ProjectName  = projectName.ToUpper();
            ViewBag.ProjectId    = projectId;
            ViewBag.CardCount    = cards.Count;
            ViewBag.CardsJson    = cardsJson;
            ViewBag.Cards        = cards;
            return View();
        }
                

        [HttpPost]
        public async Task<IActionResult> RenameQuizSession([FromBody] RenameQuizSessionRequest req)
        {
            if (!IsLoggedIn()) return Unauthorized();
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { error = "Name is required" });

            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    @"UPDATE chat_sessions cs
                    SET title = @name
                    FROM chats c
                    WHERE cs.session_id = @sid
                        AND cs.chat_id = c.chat_id
                        AND c.user_id = @uid", conn);
                cmd.Parameters.AddWithValue("name", req.Name);
                cmd.Parameters.AddWithValue("sid",  req.SessionId);
                cmd.Parameters.AddWithValue("uid",  UserId);
                int rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0) return NotFound(new { error = "Session not found" });
                return Json(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteQuizSession(int id)
        {
            if (!IsLoggedIn()) return Unauthorized();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();

                // Verify ownership before deleting
                using var check = new NpgsqlCommand(
                    @"SELECT 1 FROM chat_sessions cs
                    JOIN chats c ON c.chat_id = cs.chat_id
                    WHERE cs.session_id = @sid AND c.user_id = @uid", conn);
                check.Parameters.AddWithValue("sid", id);
                check.Parameters.AddWithValue("uid", UserId);
                if (await check.ExecuteScalarAsync() == null)
                    return NotFound(new { error = "Session not found" });

                using var delQ = new NpgsqlCommand(
                    "DELETE FROM quizzes WHERE session_id = @sid", conn);
                delQ.Parameters.AddWithValue("sid", id);
                await delQ.ExecuteNonQueryAsync();

                using var delF = new NpgsqlCommand(
                    "DELETE FROM flashcards WHERE session_id = @sid", conn);
                delF.Parameters.AddWithValue("sid", id);
                await delF.ExecuteNonQueryAsync();

                using var delS = new NpgsqlCommand(
                    "DELETE FROM chat_sessions WHERE session_id = @sid", conn);
                delS.Parameters.AddWithValue("sid", id);
                await delS.ExecuteNonQueryAsync();

                return Json(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Paste this action inside ChatController (before the closing brace of the class)
        // No new NuGet packages needed — uses PdfPig + OpenXml you already have.
        // ─────────────────────────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> DownloadQuizSession(int sessionId, string format)
        {
            if (!IsLoggedIn()) return Unauthorized();

            // ── 1. Fetch questions + session title ───────────────────────────────────
            var questions    = new List<dynamic>();
            string quizTitle = $"Quiz Session {sessionId}";

            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();

                // Verify ownership & grab title
                using var titleCmd = new NpgsqlCommand(
                    @"SELECT cs.title
                    FROM chat_sessions cs
                    JOIN chats c ON c.chat_id = cs.chat_id
                    WHERE cs.session_id = @sid AND c.user_id = @uid", conn);
                titleCmd.Parameters.AddWithValue("sid", sessionId);
                titleCmd.Parameters.AddWithValue("uid", UserId);
                var titleResult = await titleCmd.ExecuteScalarAsync();
                if (titleResult == null) return NotFound("Session not found.");
                if (titleResult != DBNull.Value && !string.IsNullOrWhiteSpace(titleResult.ToString()))
                    quizTitle = titleResult.ToString()!;

                // Fetch questions
                using var cmd = new NpgsqlCommand(
                    @"SELECT question, question_type,
                            choice_a, choice_b, choice_c, choice_d,
                            correct_answer, answer_text
                    FROM quizzes
                    WHERE session_id = @sid
                    ORDER BY quiz_id ASC", conn);
                cmd.Parameters.AddWithValue("sid", sessionId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    questions.Add(new
                    {
                        Question      = reader.GetString(0),
                        QuestionType  = reader.IsDBNull(1) ? "mcq" : reader.GetString(1),
                        ChoiceA       = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        ChoiceB       = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        ChoiceC       = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        ChoiceD       = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        CorrectAnswer = reader.IsDBNull(6) ? "" : reader.GetString(6),
                        AnswerText    = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    });
                }
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }

            if (!questions.Any()) return NotFound("No questions found for this session.");

            string safeName = string.Concat(quizTitle.Split(Path.GetInvalidFileNameChars()));

            // ── 2. Build & return file ───────────────────────────────────────────────
            if (format?.ToLower() == "pdf")
            {
                var bytes = BuildQuizPdf(quizTitle, questions);
                return File(bytes, "application/pdf", $"{safeName}.pdf");
            }
            else if (format?.ToLower() == "docx")
            {
                var bytes = BuildQuizDocx(quizTitle, questions);
                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    $"{safeName}.docx");
            }

            return BadRequest("Unsupported format. Use 'pdf' or 'docx'.");
        }

        [HttpPost]
        public async Task<IActionResult> CreateManualProject([FromBody] CreateChatRequest req)
        {
            if (!IsLoggedIn()) return Unauthorized();
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { error = "Name is required" });

            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    @"INSERT INTO projects (user_id, name, subject, created_at, is_manual)
                    VALUES (@uid, @name, @subject, @now, true)
                    RETURNING project_id", conn);
                cmd.Parameters.AddWithValue("uid", UserId);
                cmd.Parameters.AddWithValue("name", req.Name);
                cmd.Parameters.AddWithValue("subject", req.Subject ?? "");
                cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
                int newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return Json(new { id = newId, name = req.Name });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }


// ── PDF (using UglyToad.PdfPig.Writer) ───────────────────────────────────────
private static byte[] BuildQuizPdf(string title, List<dynamic> questions)
{
    // PdfPig's writer API: PdfDocumentBuilder
    var builder  = new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
    var page     = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);

    // PdfPig ships a standard font you can use without embedding files
    var font     = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);
    var fontBold = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.HelveticaBold);

    double pageW  = 595;   // A4 points
    double pageH  = 842;
    double margin = 50;
    double x      = margin;
    double y      = pageH - margin;
    double lineH  = 14;

    // Helper: add a new page when we run out of space
    UglyToad.PdfPig.Writer.PdfPageBuilder? curPage = page;
    void CheckNewPage(double needed = 0)
    {
        if (y - needed < margin)
        {
            curPage = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
            y = pageH - margin;
        }
    }

    void WriteLine(string text, double size, bool bold = false, double extraGap = 0)
    {
        CheckNewPage(size + extraGap);
        var f = bold ? fontBold : font;
        // PdfPig draws text from bottom-left; y is our running top cursor
        curPage!.AddText(text, (decimal)size, new UglyToad.PdfPig.Core.PdfPoint(x, y - size), f);
        y -= size + 3 + extraGap;
    }

    // Title
    WriteLine(title.ToUpper(), 16, bold: true, extraGap: 6);

    string[] letters = { "A", "B", "C", "D" };

    for (int i = 0; i < questions.Count; i++)
    {
        var q = questions[i];
        string qtype = (string)q.QuestionType;

        CheckNewPage(lineH * 3);
        // Question number + text (wrap naively at ~80 chars)
        string qHeader = $"Q{i + 1}. {(string)q.Question}";
        foreach (var line in WrapText(qHeader, 85))
            WriteLine(line, 10, bold: true);

        if (qtype == "fillblank")
        {
            WriteLine($"Answer: {(string)q.AnswerText}", 9);
        }
        else if (qtype == "truefalse")
        {
            string correct = (string)q.CorrectAnswer == "A" ? "True" : "False";
            WriteLine("  T  True", 9);
            WriteLine("  F  False", 9);
            WriteLine($"Correct: {correct}", 9, bold: true);
        }
        else // mcq
        {
            string[] choices = { (string)q.ChoiceA, (string)q.ChoiceB, (string)q.ChoiceC, (string)q.ChoiceD };
            string   ans     = ((string)q.CorrectAnswer).ToUpper();
            for (int ci = 0; ci < choices.Length; ci++)
            {
                if (!string.IsNullOrWhiteSpace(choices[ci]))
                    WriteLine($"  {letters[ci]}.  {choices[ci]}", 9);
            }
            string ansText = ans switch
            {
                "A" => (string)q.ChoiceA, "B" => (string)q.ChoiceB,
                "C" => (string)q.ChoiceC, "D" => (string)q.ChoiceD, _ => ans
            };
            WriteLine($"Correct: {ans}. {ansText}", 9, bold: true);
        }

        y -= 6; // gap between questions
    }

    return builder.Build();
}

// Naive word-wrap
private static IEnumerable<string> WrapText(string text, int maxChars)
{
    if (text.Length <= maxChars) { yield return text; yield break; }
    var words = text.Split(' ');
    var line  = new StringBuilder();
    foreach (var w in words)
    {
        if (line.Length + w.Length + 1 > maxChars && line.Length > 0)
        {
            yield return line.ToString();
            line.Clear();
        }
        if (line.Length > 0) line.Append(' ');
        line.Append(w);
    }
    if (line.Length > 0) yield return line.ToString();
}

// ── DOCX (using DocumentFormat.OpenXml) ──────────────────────────────────────
private static byte[] BuildQuizDocx(string title, List<dynamic> questions)
{
    using var ms  = new MemoryStream();
    using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(
                        ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);

    var mainPart = doc.AddMainDocumentPart();
    mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
    var body = mainPart.Document.AppendChild(
                   new DocumentFormat.OpenXml.Wordprocessing.Body());

    // Helper: add paragraph
    DocumentFormat.OpenXml.Wordprocessing.Paragraph AddPara(
        string text, bool bold = false, int sizePt = 11, int spacingAfter = 100)
    {
        var para = new DocumentFormat.OpenXml.Wordprocessing.Paragraph();
        var pPr  = new DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties(
                       new DocumentFormat.OpenXml.Wordprocessing.SpacingBetweenLines
                           { After = spacingAfter.ToString() });
        para.AppendChild(pPr);

        var run  = new DocumentFormat.OpenXml.Wordprocessing.Run();
        var rPr  = new DocumentFormat.OpenXml.Wordprocessing.RunProperties();
        if (bold) rPr.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Bold());
        rPr.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.FontSize
                            { Val = (sizePt * 2).ToString() });
        run.AppendChild(rPr);
        run.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Text(text)
                            { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve });
        para.AppendChild(run);
        body.AppendChild(para);
        return para;
    }

    // Title
    AddPara(title.ToUpper(), bold: true, sizePt: 16, spacingAfter: 200);

    string[] letters = { "A", "B", "C", "D" };

    for (int i = 0; i < questions.Count; i++)
    {
        var    q     = questions[i];
        string qtype = (string)q.QuestionType;

        AddPara($"Q{i + 1}. {(string)q.Question}", bold: true, spacingAfter: 60);

        if (qtype == "fillblank")
        {
            AddPara($"Answer: {(string)q.AnswerText}", bold: false, spacingAfter: 160);
        }
        else if (qtype == "truefalse")
        {
            AddPara("  T.  True",  spacingAfter: 40);
            AddPara("  F.  False", spacingAfter: 40);
            string correct = (string)q.CorrectAnswer == "A" ? "True" : "False";
            AddPara($"✓ Correct: {correct}", bold: true, spacingAfter: 160);
        }
        else // mcq
        {
            string[] choices = { (string)q.ChoiceA, (string)q.ChoiceB,
                                 (string)q.ChoiceC, (string)q.ChoiceD };
            string   ans     = ((string)q.CorrectAnswer).ToUpper();
            for (int ci = 0; ci < choices.Length; ci++)
                if (!string.IsNullOrWhiteSpace(choices[ci]))
                    AddPara($"  {letters[ci]}.  {choices[ci]}", spacingAfter: 40);

            string ansText = ans switch
            {
                "A" => (string)q.ChoiceA, "B" => (string)q.ChoiceB,
                "C" => (string)q.ChoiceC, "D" => (string)q.ChoiceD, _ => ans
            };
            AddPara($"✓ Correct: {ans}. {ansText}", bold: true, spacingAfter: 200);
        }
    }

    mainPart.Document.Save();
    doc.Dispose();
    return ms.ToArray();
}

[HttpPost]
        public async Task<IActionResult> SaveFlashcardReview([FromBody] SaveFlashcardReviewRequest req)
        {
            if (!IsLoggedIn()) return Unauthorized();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();

                // Get the flashcard_id at this index within the session
                using var idCmd = new NpgsqlCommand(
                    @"SELECT flashcard_id FROM flashcards
                      WHERE session_id = @sid
                      ORDER BY created_at ASC
                      LIMIT 1 OFFSET @offset", conn);
                idCmd.Parameters.AddWithValue("sid",    int.Parse(req.SessionId.ToString()));
                idCmd.Parameters.AddWithValue("offset", req.CardIndex);
                var idResult = await idCmd.ExecuteScalarAsync();
                if (idResult == null) return Ok(); // card not found, skip silently

                int flashcardId = Convert.ToInt32(idResult);

                // Upsert into flashcard_reviews
                using var cmd = new NpgsqlCommand(
                    @"INSERT INTO flashcard_reviews (user_id, flashcard_id, status, last_reviewed)
                      VALUES (@uid, @fid, @status, @now)
                      ON CONFLICT (user_id, flashcard_id)
                      DO UPDATE SET status = @status, last_reviewed = @now", conn);
                cmd.Parameters.AddWithValue("uid",    UserId);
                cmd.Parameters.AddWithValue("fid",    flashcardId);
                cmd.Parameters.AddWithValue("status", req.Status);
                cmd.Parameters.AddWithValue("now",    DateTime.UtcNow);
                await cmd.ExecuteNonQueryAsync();

                return Json(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        public class CreateChatRequest
        {
            public string Name    { get; set; } = "";
            public string Subject { get; set; } = "";
        }

        public IActionResult Open(int id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            ViewBag.FolderId     = id;
            ViewBag.ChatId       = -1;
            ViewBag.Username     = Username;
            ViewBag.UserInitial  = Username.Length > 0 ? Username[0].ToString().ToUpper() : "?";
            return View("Index");
        }

        private async Task SaveMessage(int chatId, string role, string content)
        {
            using var conn = RavnLearnWeb.Database.GetConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(
                "INSERT INTO messages (chat_id, role, content, timestamp) VALUES (@cid, @role, @content, @ts)", conn);
            cmd.Parameters.AddWithValue("cid", chatId);
            cmd.Parameters.AddWithValue("role", role);
            cmd.Parameters.AddWithValue("content", content);
            cmd.Parameters.AddWithValue("ts", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync();
        }

        private string ExtractPdfText(byte[] bytes)
        {
            var sb = new StringBuilder();
            try
            {
                using var doc = UglyToad.PdfPig.PdfDocument.Open(bytes);
                foreach (var page in doc.GetPages())
                {
                    var words = page.GetWords();
                    sb.AppendLine(string.Join(" ", words.Select(w => w.Text)));
                }
                string result = sb.ToString().Trim();
                if (string.IsNullOrWhiteSpace(result))
                    return "[This PDF appears to be image-based or scanned. Text could not be extracted.]";
                return result;
            }
            catch (Exception ex)
            {
                return $"[Could not extract PDF text: {ex.Message}]";
            }
        }

        private string ExtractDocxText(byte[] bytes)
        {
            try
            {
                using var ms = new MemoryStream(bytes);
                using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(ms, false);
                return doc.MainDocumentPart?.Document.Body?.InnerText ?? "";
            }
            catch { return "[Could not extract DOCX text]"; }
        }

        [HttpPost]
public async Task<IActionResult> CreateManualQuizSession([FromBody] CreateManualQuizSessionRequest req)
{
    if (!IsLoggedIn()) return Unauthorized();
    if (string.IsNullOrWhiteSpace(req.Title))
        return Json(new { error = "Title is required" });
    if (req.Questions == null || req.Questions.Count == 0)
        return Json(new { error = "No questions provided" });

    try
    {
        using var conn = RavnLearnWeb.Database.GetConnection();
        await conn.OpenAsync();

        // 1. Hanapin ang chat na linked sa project
        int chatId = 0;
        using (var chatCmd = new NpgsqlCommand(
            @"SELECT chat_id FROM chats 
              WHERE project_id = @pid AND user_id = @uid 
              LIMIT 1", conn))
        {
            chatCmd.Parameters.AddWithValue("pid", req.ProjectId);
            chatCmd.Parameters.AddWithValue("uid", UserId);
            var result = await chatCmd.ExecuteScalarAsync();

            if (result == null)
            {
                // Walang chat pa — gumawa ng bago
               using var newChat = new NpgsqlCommand(
                    @"INSERT INTO chats (user_id, project_id, name, created_at, is_manual)
                    VALUES (@uid, @pid, @name, @now, true)
                    RETURNING chat_id", conn);
                newChat.Parameters.AddWithValue("uid", UserId);
                newChat.Parameters.AddWithValue("pid", req.ProjectId);
                newChat.Parameters.AddWithValue("name", req.Title);
                newChat.Parameters.AddWithValue("now", DateTime.UtcNow);
                chatId = Convert.ToInt32(await newChat.ExecuteScalarAsync());
            }
            else
            {
                chatId = Convert.ToInt32(result);
            }
        }

        // 2. Gumawa ng chat_session
        int sessionId;
        using (var sessionCmd = new NpgsqlCommand(
            @"INSERT INTO chat_sessions (chat_id, title, created_at)
              VALUES (@cid, @title, @now)
              RETURNING session_id", conn))
        {
            sessionCmd.Parameters.AddWithValue("cid",   chatId);
            sessionCmd.Parameters.AddWithValue("title", req.Title);
            sessionCmd.Parameters.AddWithValue("now",   DateTime.UtcNow);
            sessionId = Convert.ToInt32(await sessionCmd.ExecuteScalarAsync());
        }

        // 3. I-insert ang bawat tanong
        foreach (var q in req.Questions)
        {
            using var quizCmd = new NpgsqlCommand(
                @"INSERT INTO quizzes 
                    (chat_id, session_id, question, question_type,
                     choice_a, choice_b, choice_c, choice_d,
                     correct_answer, answer_text, created_at)
                  VALUES 
                    (@cid, @sid, @q, @qtype,
                     @a, @b, @c, @d,
                     @ans, @atxt, @now)", conn);
            quizCmd.Parameters.AddWithValue("cid",   chatId);
            quizCmd.Parameters.AddWithValue("sid",   sessionId);
            quizCmd.Parameters.AddWithValue("q",     q.Question);
            quizCmd.Parameters.AddWithValue("qtype", q.QuestionType);
            quizCmd.Parameters.AddWithValue("a",     q.ChoiceA ?? "");
            quizCmd.Parameters.AddWithValue("b",     q.ChoiceB ?? "");
            quizCmd.Parameters.AddWithValue("c",     q.ChoiceC ?? "");
            quizCmd.Parameters.AddWithValue("d",     q.ChoiceD ?? "");
            quizCmd.Parameters.AddWithValue("ans",   q.CorrectAnswer ?? "");
            quizCmd.Parameters.AddWithValue("atxt",  q.AnswerText ?? "");
            quizCmd.Parameters.AddWithValue("now",   DateTime.UtcNow);
            await quizCmd.ExecuteNonQueryAsync();
        }

        return Json(new { success = true, sessionId });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }


    }

    public class CreateManualQuizSessionRequest
    {
        public int    ProjectId { get; set; }
        public string Title     { get; set; } = "";
        public List<QuizQuestionItem> Questions { get; set; } = new();
    }
    public class SendMessageRequest
    {
        public int ChatId { get; set; }
        public string Message { get; set; } = "";
    }

    public class SaveQuizRequest
    {
        public int ChatId { get; set; }
        public string QuizText { get; set; } = "";
    }

    public class RenameChatRequest
    {
        public int ChatId { get; set; }
        public string Title { get; set; } = "";
    }

        public class RenameQuizSessionRequest
        {
            public int    SessionId { get; set; }
            public string Name      { get; set; } = "";
        }
    public class SaveQuizQuestionsRequest
    {
        public int ChatId { get; set; }
        public List<QuizQuestionItem> Questions { get; set; } = new();
    }

    public class QuizQuestionItem
    {
        public string Question      { get; set; } = "";
        public string QuestionType  { get; set; } = "mcq";
        public string ChoiceA       { get; set; } = "";
        public string ChoiceB       { get; set; } = "";
        public string ChoiceC       { get; set; } = "";
        public string ChoiceD       { get; set; } = "";
        public string CorrectAnswer { get; set; } = "";
        public string AnswerText    { get; set; } = "";
    }

    public class SaveFlashcardsRequest
    {
        public int ChatId { get; set; }
        public List<FlashcardItem> Cards { get; set; } = new();
    }

    public class FlashcardItem
    {
        public string Front { get; set; } = "";
        public string Back  { get; set; } = "";
    }

    public class NewSessionRequest
    {
        public int FolderId { get; set; }
    }

    public class RenameSessionRequest
    {
        public int Id       { get; set; }
        public string Title { get; set; } = "";
    }

    public class SaveFlashcardReviewRequest
    {
        public object SessionId { get; set; } = 0;
        public int    CardIndex { get; set; }
        public string Status    { get; set; } = "";
    }

    public class RenameProjectRequest
    {
        public int Id      { get; set; }
        public string Name { get; set; } = "";
    }


}
