using GTX.Common;
using GTX.Controllers;
using GTX.Models;
using Services;
using System;
using System.Linq;
using System.Web.Mvc;

namespace GTX
{
    [RequireAdminRole]
    public class BlogsController : BaseController
    {
        public IBlogsService _blogsService { get; set; }

        public BlogsController(
            ISessionData sessionData,
            IInventoryService inventoryService,
            IVinDecoderService vinDecoderService,
            IEZ360Service _ez360Service,
            ILogService logService,
            IBlogsService blogsService, IEmployeesService employeesService)
            : base(sessionData, inventoryService, vinDecoderService, _ez360Service, logService, employeesService)
        {
            _blogsService = blogsService;
        }

        [HttpGet]
        public ActionResult Index()
        {
            var entities = _blogsService.GetAll();
            var posts = entities.Select(BlogPostModel.FromEntity).ToList();
            return View(posts);
        }

        public ActionResult List()
        {
            var blogs = _blogsService.GetAll().Where(x => x.IsPublished).OrderByDescending(x => x.CreatedAt).Select(BlogPostModel.FromEntity).ToList();
            return View(blogs.Where(m => m.IsPublished).ToList());
        }

        public ActionResult ListPartial()
        {
            var entities = _blogsService.GetAll(includeUnpublished: true);
            var posts = entities.Select(BlogPostModel.FromEntity).ToList();
            return PartialView("_BlogPost", posts);
        }

        public ActionResult Create()
        {
            var model = new BlogPostModel { IsPublished = true, CreatedAt = DateTime.Now };
            return PartialView("_BlogPostEdit", model);
        }

        public ActionResult Edit(int id)
        {
            var entity = _blogsService.GetById(id);
            if (entity == null) return HttpNotFound();

            var model = BlogPostModel.FromEntity(entity);
            return PartialView("_BlogPostEdit", model);
        }

        [HttpGet]
        public ActionResult Blog(int id)
        {
            var entity = _blogsService.GetById(id);
            if (entity == null) return HttpNotFound();

            var model = BlogPostModel.FromEntity(entity);
            return View("BlogDetails", model);
        }

        [HttpPost]
        public ActionResult Save(BlogPostModel model)
        {
            if (!ModelState.IsValid) return PartialView("_BlogPostEdit", model);

            var ok = _blogsService.Update(BlogPostModel.ToEntity(model));
            if (!ok) return HttpNotFound();

            return Json(new { ok = true });
        }

        [HttpPost]
        public ActionResult Create(BlogPostModel model)
        {
            if (!ModelState.IsValid) return PartialView("_BlogPostEdit", model);

            var ok = _blogsService.Create(BlogPostModel.ToEntity(model));
            if (!ok) return HttpNotFound();

            return Json(new { ok = true });
        }

        [HttpPost]
        public ActionResult Delete(int id)
        {
            _blogsService.Delete(id);
            return Json(new { ok = true });
        }
    }
}