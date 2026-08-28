using GTX.Common;
using GTX.Models;
using Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;

namespace GTX.Controllers
{
    [RequireAdminRole(RequiredRole = CommonUnit.Roles.Owner)]
    public sealed class ChatBotCommandsController : BaseController
    {
        private readonly IChatBotTeachingService _teachingService;

        public ChatBotCommandsController(
            ISessionData sessionData,
            IInventoryService inventoryService,
            IVinDecoderService vinDecoderService,
            ILogService logService,
            IEmployeesService employeesService,
            IChatBotTeachingService teachingService)
            : base(sessionData, inventoryService, vinDecoderService, logService, employeesService)
        {
            _teachingService = teachingService;
        }

        [HttpGet]
        public ActionResult Index()
        {
            var commands = _teachingService.GetActiveLessons()
                .Select(BuildRow)
                .ToList();

            return View(new ChatBotCommandPageModel
            {
                Commands = commands,
                Actions = ChatBotNavigationCatalog.All
            });
        }

        [HttpPost]
        public ActionResult Create(ChatBotCommandRequest request)
        {
            if (request == null || !SessionRequestToken.IsValid(Session, request.RequestToken))
                return Error("Please refresh the page and try again.", 403);

            var validation = ValidateCommandRequest(request, false);
            if (validation != null) return Error(validation);

            var normalizedPhrase = NormalizePhrase(request.Phrase);
            if (_teachingService.FindActiveLesson(normalizedPhrase) != null)
            {
                return Error("That phrase already exists. Use Edit to change its action.");
            }

            try
            {
                var lesson = _teachingService.SaveLesson(
                    request.Phrase.Trim(),
                    normalizedPhrase,
                    request.ActionKey.Trim(),
                    CommonUnit.Roles.Owner.ToString());
                return Json(new { success = true, id = lesson.Id, message = "Chatbot command created." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Unable to create chatbot command: {0}", ex);
                return Error("The command could not be created.");
            }
        }

        [HttpPost]
        public ActionResult Edit(ChatBotCommandRequest request)
        {
            if (request == null || !SessionRequestToken.IsValid(Session, request.RequestToken))
                return Error("Please refresh the page and try again.", 403);

            var validation = ValidateCommandRequest(request, true);
            if (validation != null) return Error(validation);

            try
            {
                var lesson = _teachingService.UpdateLesson(
                    request.Id,
                    request.Phrase.Trim(),
                    NormalizePhrase(request.Phrase),
                    request.ActionKey.Trim(),
                    CommonUnit.Roles.Owner.ToString());
                return Json(new { success = true, id = lesson.Id, message = "Chatbot command updated." });
            }
            catch (KeyNotFoundException ex)
            {
                return Error(ex.Message, 404);
            }
            catch (InvalidOperationException ex)
            {
                return Error(ex.Message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Unable to edit chatbot command: {0}", ex);
                return Error("The command could not be updated.");
            }
        }

        [HttpPost]
        public ActionResult Delete(int id, string requestToken)
        {
            if (!SessionRequestToken.IsValid(Session, requestToken))
                return Error("Please refresh the page and try again.", 403);

            try
            {
                if (!_teachingService.DeactivateLesson(id, CommonUnit.Roles.Owner.ToString()))
                {
                    return Error("The chatbot command was not found.", 404);
                }

                return Json(new { success = true, message = "Chatbot command deleted." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Unable to delete chatbot command: {0}", ex);
                return Error("The command could not be deleted.");
            }
        }

        private static ChatBotCommandRowModel BuildRow(ChatBotNavigationLesson lesson)
        {
            var definition = ChatBotNavigationCatalog.Find(lesson.ActionKey);

            return new ChatBotCommandRowModel
            {
                Id = lesson.Id,
                Phrase = lesson.Phrase,
                NormalizedPhrase = lesson.NormalizedPhrase,
                ActionKey = lesson.ActionKey,
                ActionLabel = definition == null ? "Missing Action" : definition.Label,
                ActionDescription = definition == null ? "This action key is not present in the navigation catalog." : definition.Description,
                CreatedByRole = lesson.CreatedByRole,
                UpdatedUtc = lesson.UpdatedUtc
            };
        }

        private static string ValidateCommandRequest(ChatBotCommandRequest request, bool requireId)
        {
            if (request == null) return "A command is required.";
            if (requireId && request.Id <= 0) return "A valid command is required.";
            if (string.IsNullOrWhiteSpace(request.Phrase)) return "Enter a phrase for the chatbot to learn.";
            if (request.Phrase.Trim().Length > 300) return "The phrase cannot exceed 300 characters.";
            if (NormalizePhrase(request.Phrase).Length == 0) return "The phrase must contain letters or numbers.";
            if (ChatBotNavigationCatalog.Find(request.ActionKey) == null) return "Choose an available action.";
            return null;
        }

        private ActionResult Error(string message, int statusCode = 400)
        {
            Response.StatusCode = statusCode;
            Response.TrySkipIisCustomErrors = true;
            return Json(new { success = false, message });
        }

        private static string NormalizePhrase(string value)
        {
            value = (value ?? string.Empty).ToLowerInvariant();
            value = Regex.Replace(value, @"[^a-z0-9]+", " ");
            return Regex.Replace(value, @"\s+", " ").Trim();
        }
    }
}
