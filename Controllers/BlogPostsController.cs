using GTX.Controllers;
using GTX.Models;
using Services;
using System;
using System.Linq;
using System.Web.Mvc;

namespace GTX
{
    public class BlogPostsController : BaseController
    {
        public IBlogPostService _blogPostService { get; set; }

        public BlogPostsController(
            ISessionData sessionData,
            IInventoryService inventoryService,
            IVinDecoderService vinDecoderService,
            IEZ360Service _ez360Service,
            ILogService logService,
            IBlogPostService blogPostService)
            : base(sessionData, inventoryService, vinDecoderService, _ez360Service, logService)
        {
            _blogPostService = blogPostService;
        }
        public ActionResult Blogs()
        {
            Model.Blogs = _blogPostService.GetAll().Where(x => x.IsPublished).OrderByDescending(x => x.CreatedAt).Select(BlogPostModel.FromEntity).ToList();
            return View(Model.Blogs.Where(m => m.IsPublished).ToList());
        }

        public ActionResult List()
        {
            var entities = _blogPostService.GetAll(includeUnpublished: true);
            var posts = entities.Select(BlogPostModel.FromEntity).ToList();
            return PartialView("_BlogPost", posts); // <-- your existing partial filename
        }

        public ActionResult Create()
        {
            var model = new BlogPostModel { IsPublished = true, CreatedAt = DateTime.Now };
            return PartialView("_BlogPostEdit", model);
        }

        public ActionResult Edit(int id)
        {
            var entity = _blogPostService.GetById(id);
            if (entity == null) return HttpNotFound();

            var model = BlogPostModel.FromEntity(entity);
            return PartialView("_BlogPostEdit", model);
        }

        [HttpGet]
        public ActionResult Blog(int id)
        {
            var entity = _blogPostService.GetById(id);
            if (entity == null) return HttpNotFound();

            var model = BlogPostModel.FromEntity(entity);
            return View("BlogDetails", model);
        }

        [HttpPost]
        public ActionResult Save(BlogPostModel model)
        {
            if (!ModelState.IsValid) return PartialView("_BlogPostEdit", model);

            var ok = _blogPostService.Update(BlogPostModel.ToEntity(model));
            if (!ok) return HttpNotFound();

            return Json(new { ok = true });
        }

        [HttpPost]
        public ActionResult Create(BlogPostModel model)
        {
            if (!ModelState.IsValid) return PartialView("_BlogPostEdit", model);

            var ok = _blogPostService.Create(BlogPostModel.ToEntity(model));
            if (!ok) return HttpNotFound();

            return Json(new { ok = true });
        }

        [HttpPost]
        public ActionResult Delete(int id)
        {
            _blogPostService.Delete(id);
            return Json(new { ok = true });
        }
    }
}