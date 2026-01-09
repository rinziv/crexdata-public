from skimage.measure import label, regionprops
import numpy as np
import cv2 as cv


class MorphProcessing:
    def __init__(self, THRESHOLD, IMCLOSING):
        self.THRESHOLD = THRESHOLD
        self.IMCLOSING = IMCLOSING

    def morphProcessing(self, sourceImg):
        # Binarize
        binarized = sourceImg > self.THRESHOLD

        # Divide in regions and keep only the biggest
        label_img = label(binarized)
        regions = regionprops(label_img)

        if len(regions) == 0:
            morph = np.zeros(sourceImg.shape, dtype="uint8")
            centroid = [np.nan, np.nan]
            return (morph, centroid)
        regions.sort(key=lambda x: x.area, reverse=True)
        centroid = list(
            regions[0].centroid
        )  # centroid coordinates of the biggest object

        if len(regions) > 1:
            for rg in regions[1:]:
                label_img[rg.coords[:, 0], rg.coords[:, 1]] = 0

        label_img[label_img != 0] = 1

        biggestRegion = (label_img * 255).astype(np.uint8)
        # Morphological
        kernel = cv.getStructuringElement(
            cv.MORPH_ELLIPSE, (self.IMCLOSING, self.IMCLOSING)
        )
        morph = cv.morphologyEx(biggestRegion, cv.MORPH_CLOSE, kernel)
        return (morph, centroid)
