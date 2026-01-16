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
        public BlogPostsController(
            ISessionData sessionData,
            IInventoryService inventoryService,
            IVinDecoderService vinDecoderService,
            IEZ360Service _ez360Service,
            ILogService logService,
            IBlogPostService blogPostService)
            : base(sessionData, inventoryService, vinDecoderService, _ez360Service, logService, blogPostService)
        {
        }
        public ActionResult Blogs()
        {
            Model.Blogs = BlogPostService.GetAll().Where(x => x.IsPublished).OrderByDescending(x => x.CreatedAt).Select(BlogPostModel.FromEntity).ToList();
            return View(Model.Blogs.Where(m => m.IsPublished).ToList());
        }

        public ActionResult List()
        {
            var entities = BlogPostService.GetAll(includeUnpublished: true);
            var posts = entities.Select(BlogPostModel.FromEntity).ToList();
            return PartialView("_BlogPost", posts); // <-- your existing partial filename
        }

        public ActionResult Create()
        {
            var vm = new BlogPostModel { IsPublished = true, CreatedAt = DateTime.Now };
            return PartialView("_BlogPostEdit", vm);
        }

        public ActionResult Edit(int id)
        {
            var entity = BlogPostService.GetById(id);
            if (entity == null) return HttpNotFound();

            var vm = BlogPostModel.FromEntity(entity);
            return PartialView("_BlogPostEdit", vm);
        }

        [HttpPost]
        public ActionResult Save(BlogPostModel vm)
        {
            if (!ModelState.IsValid) return PartialView("_BlogPostEdit", vm);

            var ok = BlogPostService.Update(BlogPostModel.ToEntity(vm));
            if (!ok) return HttpNotFound();

            return Json(new { ok = true });
        }

        [HttpPost]
        public ActionResult Create(BlogPostModel vm)
        {
            if (!ModelState.IsValid) return PartialView("_BlogPostEdit", vm);

            var ok = BlogPostService.Create(BlogPostModel.ToEntity(vm));
            if (!ok) return HttpNotFound();

            return Json(new { ok = true });
        }

        [HttpPost]
        public ActionResult Delete(int id)
        {
            BlogPostService.Delete(id);
            return Json(new { ok = true });
        }
    }
}