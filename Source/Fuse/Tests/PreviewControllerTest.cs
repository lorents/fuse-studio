using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fuse.Preview;
using NSubstitute;
using NUnit.Framework;
using Outracks.Fuse.Live;
using Outracks.Fuse.Model;
using Outracks.Simulator;

namespace Outracks.Fuse
{
	[TestFixture]
	class PreviewControllerTest
	{
		[Test]
		void Failed_updates_causes_reify_on_Flush()
		{
			var preview = Substitute.For<IPreview>();
			var status = Substitute.For<IStatus>();
			var element = Substitute.For<ElementModel>();

			preview.TryUpdateAttribute(Arg.Any<ObjectIdentifier>(), Arg.Any<string>(), Arg.Any<string>())
				.Returns(false);
			
			var controller = new PreviewController(preview, status);

			controller.ElementAttributeChanged(element, "attr");
			Assert.That(controller.NeedsFlush, Is.True);

			preview.DidNotReceive().Refresh();
			controller.Flush();
			preview.Received().Refresh();

			Assert.That(controller.NeedsFlush, Is.False);
		}
	}


	class AttributeEditorTest
	{
		IDocument<byte[]> file;
		ElementModel element;
		PreviewController preview;
		ElementAttributeProperty attribute;

		[SetUp]
		void SetUp()
		{
			file = Substitute.For<IDocument<byte[]>>();
			element = new RootElement(new DocumentModel(file));
			preview = Substitute.For<PreviewController>();
			attribute = new ElementAttributeProperty(element, "attr", preview);
		}

		[Test]
		void Write_changes_model()
		{
			attribute.Write("foo", save: false);
			Assert.That(element["attr"].Value, Is.EqualTo("foo"));
		}

		[Test]
		void Write_updates_preview()
		{
			attribute.Write("foo", save: false);
			preview.Received().ElementAttributeChanged(element, "attr");

			attribute.Write("foo", save: true);
			preview.Received().ElementAttributeChanged(element, "attr");
		}

		[Test]
		void Write_flushes_preview_iff_save_is_true()
		{
			attribute.Write("foo", save: false);
			preview.DidNotReceive().Flush();

			attribute.Write("foo", save: true);
			preview.Received().Flush();

		}

		[Test]
		void Write_saves_document_before_flushing_preview()
		{
			attribute.Write("foo", save: true);

			element.Document.File.Received().Save(Arg.Any<byte[]>());

			// TODO: test order of calls to file vs flush (since both touch disk)
		}

		void Status_is_NeedsUpdate_whenever_update_fails_until_refresh_happens()
		{

		}

		void Status_is_HasErrors_whenever_update_fails_until_refresh_happens()
		{

		}
	}

	class ElementUpdaterTest
	{
		void Update_changes_model()
		{

		}

		void Update_updates_preview()
		{

		}

		void Update_does_not_flush_preview()
		{

		}

		void Flush_flushes_preview()
		{

		}

		void Neither_method_saves_to_disk()
		{

		}
	}

}
