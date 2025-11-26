use spade::{DelaunayTriangulation, Point2, Triangulation};

fn main() -> Result<(), spade::InsertionError> {
    let mut triangulation: DelaunayTriangulation<Point2<f64>> = DelaunayTriangulation::new();

    let mut points = Vec::new();
    for y in 0..=2 {
        for x in 0..=2 {
            points.push(Point2::new(x as f64, y as f64));
        }
    }

    for p in &points {
        triangulation.insert(*p)?;
    }

    let mut triangles: Vec<[usize; 3]> = Vec::new();

    for face in triangulation.inner_faces() {
        let vertices = face.vertices();
        let mut idx = [0usize; 3];

        for (i, v) in vertices.iter().enumerate() {
            let pos = v.position();
            let x = pos.x as usize;
            let y = pos.y as usize;
            idx[i] = y * 3 + x;
        }

        idx.sort();
        triangles.push(idx);
    }

    triangles.sort();

    println!("3x3 grid triangles (indices into 0..8 in row-major order):");
    for t in &triangles {
        println!("[{}, {}, {}]", t[0], t[1], t[2]);
    }

    Ok(())
}
